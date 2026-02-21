using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

/// <summary>
/// Manages the Waitlist — a behaviour-psychology tool that forces a cooling-off
/// period before purchase and calculates a Purchase Readiness Score.
///
/// Readiness Score breakdown (max 100):
///   Budget Health     0–20  — how far under/over your buckets are this month
///   Savings Progress  0–20  — how full your savings bucket is vs. its allocation
///   Debt Load         0–20  — monthly debt obligations relative to income
///   Emotional State   0–15  — emotion recorded when the item was added
///   Item Priority     0–15  — how important the item is to you
///   Time Waited       0–10  — patience bonus; longer wait = higher confidence
/// </summary>
public sealed class WaitlistService : IWaitlistService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    public WaitlistService(IDbContextFactory<CoinStackDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CRUD
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<List<WaitlistItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.WaitlistItems
            .AsNoTracking()
            .Where(x => !x.IsPurchased)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.CoolOffUntil)
            .ToListAsync(cancellationToken);
    }

    public async Task<WaitlistItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.WaitlistItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<WaitlistItem> CreateAsync(WaitlistItem item, CancellationToken cancellationToken = default)
    {
        item.CoolOffUntil = DateTime.UtcNow.Add(CoolOffDuration(item.CoolOffPeriod));
        item.IsUnlocked = false;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.WaitlistItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task UpdateAsync(WaitlistItem item, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.WaitlistItems.FirstOrDefaultAsync(x => x.Id == item.Id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.Name = item.Name;
        existing.Description = item.Description;
        existing.EstimatedCost = item.EstimatedCost;
        existing.Url = item.Url;
        existing.Priority = item.Priority;
        existing.EmotionAtTimeOfAdding = item.EmotionAtTimeOfAdding;
        existing.ReflectionNote = item.ReflectionNote;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.WaitlistItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        db.WaitlistItems.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkPurchasedAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.WaitlistItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.IsPurchased = true;
        existing.PurchasedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cool-off
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks all locked items and unlocks any whose cool-off period has expired.
    /// Call this on page load so the UI reflects the latest state.
    /// </summary>
    public async Task EvaluateCoolOffsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var locked = await db.WaitlistItems
            .Where(x => !x.IsUnlocked && !x.IsPurchased && x.CoolOffUntil <= now)
            .ToListAsync(cancellationToken);

        if (locked.Count == 0)
        {
            return;
        }

        foreach (var item in locked)
        {
            item.IsUnlocked = true;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Purchase Readiness Score
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates and persists a readiness score for the given item.
    /// Returns the computed score (0–100).
    /// </summary>
    public async Task<int> CalculateReadinessScoreAsync(int itemId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await db.WaitlistItems.FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken);
        if (item is null)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var thisYear = now.Year;
        var thisMonth = now.Month;

        // ── 1. Budget Health (0–20) ──────────────────────────────────────────
        // Compare actual spending against bucket allocations this month.
        var buckets = await db.Buckets
            .AsNoTracking()
            .Where(x => !x.IsSavings)
            .ToListAsync(cancellationToken);

        var monthlyExpenses = await db.Transactions
            .AsNoTracking()
            .Where(x => x.Type == TransactionType.Expense
                     && x.OccurredAtUtc.Year == thisYear
                     && x.OccurredAtUtc.Month == thisMonth
                     && x.BucketId != null)
            .GroupBy(x => x.BucketId!.Value)
            .Select(g => new { BucketId = g.Key, Spent = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);

        var monthlyExpensesDict = monthlyExpenses.ToDictionary(e => e.BucketId, e => e.Spent);

        int overCount = 0;
        int totalBuckets = buckets.Count;
        foreach (var bucket in buckets)
        {
            var spent = monthlyExpensesDict.GetValueOrDefault(bucket.Id);
            if (spent > bucket.AllocatedAmount)
            {
                overCount++;
            }
        }

        int budgetScore = totalBuckets == 0
            ? 10
            : (int)Math.Round(20.0 * (1.0 - (double)overCount / totalBuckets));

        // ── 2. Savings Progress (0–20) ───────────────────────────────────────
        // How full is the savings bucket vs. its allocation?
        var savingsBucket = await db.Buckets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsSavings, cancellationToken);

        int savingsScore = 10; // neutral default

        if (savingsBucket is not null && savingsBucket.AllocatedAmount > 0)
        {
            var savedThisMonth = await db.Transactions
                .AsNoTracking()
                .Where(x => x.BucketId == savingsBucket.Id
                         && x.OccurredAtUtc.Year == thisYear
                         && x.OccurredAtUtc.Month == thisMonth)
                .SumAsync(x => x.Amount, cancellationToken);

            var savingsRate = (double)(savedThisMonth / savingsBucket.AllocatedAmount);

            savingsScore = savingsRate >= 1.0 ? 20
                         : savingsRate >= 0.75 ? 17
                         : savingsRate >= 0.5 ? 13
                         : savingsRate >= 0.25 ? 8
                         : 3;
        }

        // ── 3. Debt Load (0–20) ──────────────────────────────────────────────
        // Monthly debt obligations vs. monthly income.
        var debts = await db.DebtAccounts
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var monthlyIncome = await db.Transactions
            .AsNoTracking()
            .Where(x => x.Type == TransactionType.Income
                     && x.OccurredAtUtc.Year == thisYear
                     && x.OccurredAtUtc.Month == thisMonth)
            .SumAsync(x => x.Amount, cancellationToken);

        var totalMonthlyDebt = debts.Sum(d => d.MonthlyPaymentAmount);

        int debtScore;
        if (monthlyIncome <= 0 || totalMonthlyDebt <= 0)
        {
            debtScore = debts.Count == 0 ? 20 : 10;
        }
        else
        {
            var debtToIncomeRatio = (double)(totalMonthlyDebt / monthlyIncome);
            debtScore = debtToIncomeRatio < 0.15 ? 20
                      : debtToIncomeRatio < 0.25 ? 16
                      : debtToIncomeRatio < 0.35 ? 11
                      : debtToIncomeRatio < 0.50 ? 6
                      : 2;
        }

        // ── 4. Emotional State (0–15) ────────────────────────────────────────
        // Use the emotion recorded when the item was added. If none, check
        // the most recent completed reflection for context.
        var emotion = item.EmotionAtTimeOfAdding;
        if (emotion is null)
        {
            var latestReflection = await db.Reflections
                .AsNoTracking()
                .Where(x => x.IsCompleted && x.EmotionTag.HasValue)
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            emotion = latestReflection?.EmotionTag;
        }

        int emotionScore = emotion switch
        {
            EmotionTag.Motivated or EmotionTag.Proud => 15,
            EmotionTag.Excited => 12,
            EmotionTag.Neutral => 10,
            EmotionTag.Bored or EmotionTag.Tired => 6,
            EmotionTag.Stressed or EmotionTag.Anxious => 4,
            EmotionTag.Impulsive or EmotionTag.Guilty => 2,
            null => 8,
            _ => 8
        };

        // ── 5. Item Priority (0–15) ──────────────────────────────────────────
        int priorityScore = item.Priority switch
        {
            WaitlistPriority.High => 15,
            WaitlistPriority.Medium => 10,
            WaitlistPriority.Low => 4,
            _ => 4
        };

        // ── 6. Time Waited (0–10) ────────────────────────────────────────────
        // Patience is a virtue — the longer it has been on the list, the more
        // considered the decision is.
        var daysWaited = (now - item.CreatedAtUtc).TotalDays;
        int timeScore = daysWaited >= 30 ? 10
                      : daysWaited >= 14 ? 8
                      : daysWaited >= 7 ? 6
                      : daysWaited >= 3 ? 3
                      : 0;

        // ── Aggregate ────────────────────────────────────────────────────────
        var total = Math.Clamp(budgetScore + savingsScore + debtScore + emotionScore + priorityScore + timeScore, 0, 100);

        item.Score = total;
        item.LastEvaluated = now;
        await db.SaveChangesAsync(cancellationToken);

        return total;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static TimeSpan CoolOffDuration(CoolOffPeriod period) => period switch
    {
        CoolOffPeriod.Hours24 => TimeSpan.FromHours(24),
        CoolOffPeriod.Days3 => TimeSpan.FromDays(3),
        CoolOffPeriod.Days7 => TimeSpan.FromDays(7),
        CoolOffPeriod.Days30 => TimeSpan.FromDays(30),
        _ => TimeSpan.FromDays(7)
    };

    public static string CoolOffLabel(CoolOffPeriod period) => period switch
    {
        CoolOffPeriod.Hours24 => "24 hours",
        CoolOffPeriod.Days3 => "3 days",
        CoolOffPeriod.Days7 => "7 days",
        CoolOffPeriod.Days30 => "30 days",
        _ => "7 days"
    };
}
