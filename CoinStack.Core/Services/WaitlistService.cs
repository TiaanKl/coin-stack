using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class WaitlistService : IWaitlistService
{
    private const int ImpulseResistedPoints = 8;

    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;
    private readonly ISettingsService _settingsService;
    private readonly IScoringService _scoringService;
    private readonly ITransactionService _transactionService;

    public WaitlistService(
        IDbContextFactory<CoinStackDbContext> dbFactory,
        ISettingsService settingsService,
        IScoringService scoringService,
        ITransactionService transactionService)
    {
        _dbFactory = dbFactory;
        _settingsService = settingsService;
        _scoringService = scoringService;
        _transactionService = transactionService;
    }

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

    public async Task<(WaitlistItem? Item, GameTransactionResult? Result)> MarkPurchasedAsync(
        int id,
        int? bucketId = null,
        int? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.WaitlistItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null || existing.IsPurchased)
        {
            return (null, null);
        }

        existing.IsPurchased = true;
        existing.PurchasedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var transaction = new Transaction
        {
            Description = existing.Name,
            Amount = existing.EstimatedCost,
            Type = TransactionType.Expense,
            Notes = existing.ReflectionNote,
            BucketId = bucketId,
            CategoryId = categoryId,
            ExpenseKind = ExpenseKind.Discretionary,
            IsImpulse = true,
            OccurredAtUtc = DateTime.UtcNow,
        };

        var (_, gameResult) = await _transactionService.CreateWithGameLoopAsync(transaction, 0, cancellationToken);
        // NoImpulseBuy streak is reset inside ScoringService when IsImpulse is scored.

        return (existing, gameResult);
    }

    public async Task MarkResistedAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.WaitlistItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null || existing.IsPurchased)
        {
            return;
        }

        var itemName = existing.Name;
        db.WaitlistItems.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);

        var settings = await _settingsService.GetAsync(cancellationToken);
        if (settings.EnableScoring)
        {
            await _scoringService.AddScoreEventAsync(
                ImpulseResistedPoints,
                ScoreChangeReason.ImpulseResisted,
                $"Resisted impulse: {itemName}",
                cancellationToken: cancellationToken);
        }

        if (settings.EnableStreaks)
        {
            await IncrementNoImpulseBuyStreakAsync(cancellationToken);
        }
    }

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

    public async Task<int> CalculateReadinessScoreAsync(int itemId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await db.WaitlistItems.FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken);
        if (item is null)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var settings = await _settingsService.GetAsync(cancellationToken);
        var (periodStartUtc, periodEndUtc) = GetBudgetPeriodBoundsUtc(settings.MonthStartDay, now);

        var buckets = await db.Buckets
            .AsNoTracking()
            .Where(x => !x.IsSavings)
            .ToListAsync(cancellationToken);

        var monthlyExpenses = await db.Transactions
            .AsNoTracking()
            .Where(x => x.Type == TransactionType.Expense
                     && x.OccurredAtUtc >= periodStartUtc
                     && x.OccurredAtUtc < periodEndUtc
                     && x.BucketId != null)
            .GroupBy(x => x.BucketId!.Value)
            .Select(g => new { BucketId = g.Key, Spent = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);

        var expenseByBucket = monthlyExpenses.ToDictionary(e => e.BucketId, e => e.Spent);

        int overCount = 0;
        int totalBuckets = buckets.Count;
        foreach (var bucket in buckets)
        {
            var spent = expenseByBucket.GetValueOrDefault(bucket.Id, 0m);
            if (spent > bucket.AllocatedAmount)
            {
                overCount++;
            }
        }

        int budgetScore = totalBuckets == 0
            ? 10
            : (int)Math.Round(20.0 * (1.0 - (double)overCount / totalBuckets));

        var savingsBucket = await db.Buckets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsSavings, cancellationToken);

        int savingsScore = 10; // neutral default

        if (savingsBucket is not null && savingsBucket.AllocatedAmount > 0)
        {
            var monthlySavingsTransactions = await db.Transactions
                .AsNoTracking()
                .Where(x => x.BucketId == savingsBucket.Id
                         && x.OccurredAtUtc >= periodStartUtc
                         && x.OccurredAtUtc < periodEndUtc)
                .ToListAsync(cancellationToken);

            var netSavedThisMonth = monthlySavingsTransactions.Sum(x =>
                x.Type == TransactionType.Expense ? -x.Amount : x.Amount);

            if (netSavedThisMonth < 0)
            {
                netSavedThisMonth = 0;
            }

            var savingsRate = (double)(netSavedThisMonth / savingsBucket.AllocatedAmount);

            savingsScore = savingsRate >= 1.0 ? 20
                         : savingsRate >= 0.75 ? 17
                         : savingsRate >= 0.5 ? 13
                         : savingsRate >= 0.25 ? 8
                         : 3;
        }

        var debts = await db.DebtAccounts
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var monthlyIncome = await db.Transactions
            .AsNoTracking()
            .Where(x => x.Type == TransactionType.Income
                     && x.OccurredAtUtc >= periodStartUtc
                     && x.OccurredAtUtc < periodEndUtc)
            .SumAsync(x => x.Amount, cancellationToken);

        if (monthlyIncome <= 0 && settings.MonthlyIncome > 0)
        {
            monthlyIncome = settings.MonthlyIncome;
        }

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

        int priorityScore = item.Priority switch
        {
            WaitlistPriority.High => 15,
            WaitlistPriority.Medium => 10,
            WaitlistPriority.Low => 4,
            _ => 4
        };

        var daysWaited = (now - item.CreatedAtUtc).TotalDays;
        int timeScore = daysWaited >= 30 ? 10
                      : daysWaited >= 14 ? 8
                      : daysWaited >= 7 ? 6
                      : daysWaited >= 3 ? 3
                      : 0;

        var total = Math.Clamp(budgetScore + savingsScore + debtScore + emotionScore + priorityScore + timeScore, 0, 100);

        item.Score = total;
        item.LastEvaluated = now;
        await db.SaveChangesAsync(cancellationToken);

        return total;
    }

    public async Task<string?> GetCostFramingMessageAsync(decimal estimatedCost, CancellationToken cancellationToken = default)
    {
        if (estimatedCost <= 0m)
        {
            return null;
        }

        var settings = await _settingsService.GetAsync(cancellationToken);
        var monthlyContribution = settings.SavingsIsPercent
            ? settings.MonthlyIncome * (settings.MonthlySavingsPercent / 100m)
            : settings.MonthlySavingsAmount;

        if (monthlyContribution > 0m)
        {
            var daysOfSavings = (int)Math.Ceiling((double)(estimatedCost / monthlyContribution * 30m));
            if (daysOfSavings < 1)
            {
                daysOfSavings = 1;
            }

            return $"This is about {daysOfSavings} day{(daysOfSavings == 1 ? "" : "s")} of your savings contributions.";
        }

        if (settings.MonthlyIncome > 0m)
        {
            var pctOfIncome = FinancialEngine.RoundCurrency(estimatedCost / settings.MonthlyIncome * 100m);
            return $"This is {pctOfIncome}% of your monthly income.";
        }

        return null;
    }

    public async Task<int> GetNoImpulseBuyStreakAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var streak = await db.Streaks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Type == StreakType.NoImpulseBuy, cancellationToken);
        return streak?.CurrentCount ?? 0;
    }

    public async Task ResetNoImpulseBuyStreakAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var streak = await db.Streaks.FirstOrDefaultAsync(x => x.Type == StreakType.NoImpulseBuy, cancellationToken);
        if (streak is null)
        {
            return;
        }

        streak.CurrentCount = 0;
        streak.LastIncrementedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task IncrementNoImpulseBuyStreakAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var streak = await db.Streaks.FirstOrDefaultAsync(x => x.Type == StreakType.NoImpulseBuy, cancellationToken);

        if (streak is null)
        {
            db.Streaks.Add(new Streak
            {
                Type = StreakType.NoImpulseBuy,
                CurrentCount = 1,
                BestCount = 1,
                LastIncrementedAtUtc = now,
            });
        }
        else
        {
            streak.CurrentCount++;
            streak.BestCount = Math.Max(streak.BestCount, streak.CurrentCount);
            streak.LastIncrementedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static TimeSpan CoolOffDuration(CoolOffPeriod period) => period switch
    {
        CoolOffPeriod.Hours24 => TimeSpan.FromHours(24),
        CoolOffPeriod.Days3 => TimeSpan.FromDays(3),
        CoolOffPeriod.Days7 => TimeSpan.FromDays(7),
        CoolOffPeriod.Days30 => TimeSpan.FromDays(30),
        _ => TimeSpan.FromDays(7)
    };

    private static (DateTime StartUtc, DateTime EndUtc) GetBudgetPeriodBoundsUtc(int monthStartDay, DateTime utcNow)
    {
        if (monthStartDay is < 1 or > 28)
        {
            monthStartDay = 1;
        }

        var startThisMonth = new DateTime(utcNow.Year, utcNow.Month, monthStartDay, 0, 0, 0, DateTimeKind.Utc);
        var startUtc = utcNow.Day >= monthStartDay ? startThisMonth : startThisMonth.AddMonths(-1);
        var endUtc = startUtc.AddMonths(1);
        return (startUtc, endUtc);
    }

    public static string CoolOffLabel(CoolOffPeriod period) => period switch
    {
        CoolOffPeriod.Hours24 => "24 hours",
        CoolOffPeriod.Days3 => "3 days",
        CoolOffPeriod.Days7 => "7 days",
        CoolOffPeriod.Days30 => "30 days",
        _ => "7 days"
    };
}
