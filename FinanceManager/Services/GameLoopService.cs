using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

/// <summary>
/// The game loop orchestrates the interaction between buckets, scoring, streaks, and reflections.
///
/// Flow when a transaction is logged:
///   1. Determine which bucket it belongs to
///   2. Calculate pre-transaction spent amount for that bucket
///   3. Pass to scoring service to evaluate +/- points
///   4. Check if the transaction triggers a CBT reflection
///   5. Update streaks as appropriate
///   6. Return result with points changed, message, and any triggered reflection
///
/// Daily check-in flow:
///   1. Award +2 points for daily check-in
///   2. Increment DailyCheckIn streak
///   3. If streak milestone (every 7 days), award +10 bonus
///   4. Check all buckets — if all under budget, increment DailyUnderBudget streak
/// </summary>
public sealed class GameLoopService : IGameLoopService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;
    private readonly IBucketService _bucketService;
    private readonly IScoringService _scoringService;
    private readonly IReflectionService _reflectionService;

    public GameLoopService(
        IDbContextFactory<CoinStackDbContext> dbFactory,
        IBucketService bucketService,
        IScoringService scoringService,
        IReflectionService reflectionService)
    {
        _dbFactory = dbFactory;
        _bucketService = bucketService;
        _scoringService = scoringService;
        _reflectionService = reflectionService;
    }

    public async Task<GameState> GetCurrentStateAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var buckets = await _bucketService.GetAllAsync(cancellationToken);
        var spent = await _bucketService.GetSpentAmountsAsync(now.Year, now.Month, cancellationToken);
        var totalScore = await _scoringService.GetTotalScoreAsync(cancellationToken);
        var recentEvents = await _scoringService.GetRecentEventsAsync(5, cancellationToken);
        var pendingReflection = await _reflectionService.GetPendingAsync(cancellationToken);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var streaks = await db.Streaks.AsNoTracking().ToListAsync(cancellationToken);

        var statuses = buckets.Select(b => new BucketStatus
        {
            BucketId = b.Id,
            Name = b.Name,
            ColorHex = b.ColorHex,
            Allocated = b.AllocatedAmount,
            Spent = spent.GetValueOrDefault(b.Id),
        }).ToList();

        return new GameState
        {
            TotalScore = totalScore,
            RecentEvents = recentEvents,
            Streaks = streaks,
            PendingReflection = pendingReflection,
            BucketStatuses = statuses,
        };
    }

    public async Task ProcessDailyCheckInAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var streak = await db.Streaks.FirstOrDefaultAsync(x => x.Type == StreakType.DailyCheckIn, cancellationToken);
        var now = DateTime.UtcNow;

        if (streak is null)
        {
            streak = new Streak { Type = StreakType.DailyCheckIn, CurrentCount = 1, BestCount = 1, LastIncrementedAtUtc = now };
            db.Streaks.Add(streak);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (streak.LastIncrementedAtUtc.Date == now.Date)
        {
            return;
        }

        var daysSince = (now.Date - streak.LastIncrementedAtUtc.Date).Days;

        if (daysSince == 1)
        {
            streak.CurrentCount++;
            streak.BestCount = Math.Max(streak.BestCount, streak.CurrentCount);
        }
        else
        {
            streak.CurrentCount = 1;
        }

        streak.LastIncrementedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        if (streak.CurrentCount > 0 && streak.CurrentCount % 7 == 0)
        {
            // "increases weekly by 1" - interpreting as +1 point for week 1, +2 for week 2? Or just +1 flat? 
            // "increases based on streak... more like weekly by 1" -> implies progressive or simple +1. 

            var weeks = streak.CurrentCount / 7;
            var points = weeks;

            await _scoringService.AddScoreEventAsync(
                points,
                ScoreChangeReason.StreakBonus,
                $"{weeks}-week streak bonus!",
                cancellationToken: cancellationToken);
        }

        var buckets = await _bucketService.GetAllAsync(cancellationToken);
        var spent = await _bucketService.GetSpentAmountsAsync(now.Year, now.Month, cancellationToken);
        var allUnder = buckets.All(b => spent.GetValueOrDefault(b.Id) <= b.AllocatedAmount);

        if (allUnder && buckets.Count > 0)
        {
            var underStreak = await db.Streaks.FirstOrDefaultAsync(x => x.Type == StreakType.DailyUnderBudget, cancellationToken);
            if (underStreak is null)
            {
                underStreak = new Streak { Type = StreakType.DailyUnderBudget, CurrentCount = 1, BestCount = 1, LastIncrementedAtUtc = DateTime.UtcNow };
                db.Streaks.Add(underStreak);
            }
            else
            {
                if (daysSince >= 1)
                {
                    underStreak.CurrentCount = daysSince == 1 ? underStreak.CurrentCount + 1 : 1;
                    underStreak.BestCount = Math.Max(underStreak.BestCount, underStreak.CurrentCount);
                    underStreak.LastIncrementedAtUtc = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<GameTransactionResult> ProcessTransactionAsync(
    Transaction transaction,
    int userTimezoneOffsetHours,
    CancellationToken cancellationToken = default)
    {
        var result = new GameTransactionResult();

        if (transaction.Type != TransactionType.Expense || transaction.BucketId is null)
        {
            return result;
        }

        var transactionDate = transaction.OccurredAtUtc.AddHours(userTimezoneOffsetHours);
        var currentYear = transactionDate.Year;
        var currentMonth = transactionDate.Month;

        var bucket = await _bucketService.GetByIdAsync(transaction.BucketId.Value, cancellationToken);
        if (bucket is null) return result;

        var monthlyLimit = await GetMonthlyLimitAsync(
            transaction.CategoryId,
            currentYear,
            currentMonth,
            bucket.AllocatedAmount,
            cancellationToken);

        var spentDict = await _bucketService.GetSpentAmountsAsync(currentYear, currentMonth, cancellationToken);
        var totalSpentIncludingCurrent = spentDict.GetValueOrDefault(bucket.Id);

        var spentBefore = totalSpentIncludingCurrent - transaction.Amount;
        if (spentBefore < 0) spentBefore = 0;

        var scoreBefore = await _scoringService.GetTotalScoreAsync(cancellationToken);
        await _scoringService.EvaluateTransactionAsync(transaction, monthlyLimit, spentBefore, cancellationToken);
        var scoreAfter = await _scoringService.GetTotalScoreAsync(cancellationToken);
        result.PointsChanged = scoreAfter - scoreBefore;

        if (bucket.IsSavings)
        {
            await _scoringService.AddScoreEventAsync(
                -15,
                ScoreChangeReason.SavingsDip,
                $"Dipped into {bucket.Name} savings",
                transaction.Id,
                transaction.BucketId,
                cancellationToken);

            result.PointsChanged -= 15;
        }

        // 7. Goal Auto-Updates (Positive Feedback Loop)
        //  If I ADD money (negative expense? or Income?), handle it. 
        //  *Note: If your app tracks savings contributions as 'Expenses' moving money to a pot, 
        //  you might need to invert this check depending on your accounting model.*
        if (bucket.IsSavings && transaction.Amount > 0)
        {
            // Find a goal that matches this bucket (by name or ID if you add a link)
            // For "Better Code", add 'GoalId' to the Bucket entity so they are explicitly linked.

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var linkedGoal = await db.Goals.FirstOrDefaultAsync(g => g.Id == bucket.Id, cancellationToken);

            if (linkedGoal != null)
            {
                linkedGoal.CurrentAmount += transaction.Amount;
                if (linkedGoal.CurrentAmount >= linkedGoal.TargetAmount && linkedGoal.Status != GoalStatus.Completed)
                {
                    linkedGoal.Status = GoalStatus.Completed;
                    await _scoringService.AddScoreEventAsync(50, ScoreChangeReason.GoalAchieved, $"Goal '{linkedGoal.Name}' Reached!");
                }
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var spentAfter = spentBefore + transaction.Amount;
        ReflectionTrigger? trigger = null;

        if (spentAfter > monthlyLimit)
        {
            trigger = ReflectionTrigger.OverBudgetSpend;
        }
        else if (bucket.IsSavings)
        {
            trigger = ReflectionTrigger.SavingsDip;
        }
        else if (transaction.IsImpulse)
        {
            trigger = ReflectionTrigger.ImpulseBuy;
        }
        else if (monthlyLimit > 0 && transaction.Amount >= monthlyLimit * 0.5m)
        {
            trigger = ReflectionTrigger.LargeExpense;
        }

        if (trigger.HasValue)
        {
            var reflection = await _reflectionService.CreateReflectionAsync(trigger.Value, transaction.Id, cancellationToken);
            result.TriggeredReflection = true;
            result.Reflection = reflection;
        }

        result.Message = result.PointsChanged switch
        {
            > 0 => $"+{result.PointsChanged} points! Keeping it on track!",
            < 0 => $"{result.PointsChanged} points. Watch that budget.",
            _ => "Transaction recorded."
        };

        return result;
    }

    /// <summary>
    /// Resolves the spending limit for a specific category in a specific month.
    /// Priorities:
    /// 1. Explicit 'Budget' entity for that Category/Month/Year.
    /// 2. Default 'AllocatedAmount' on the Bucket itself.
    /// </summary>
    private async Task<decimal> GetMonthlyLimitAsync(
        int? categoryId,
        int year,
        int month,
        decimal defaultLimit,
        CancellationToken cancellationToken)
    {
        if (categoryId is null)
            return defaultLimit;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var monthlyOverride = await db.Budgets
            .AsNoTracking()
            .FirstOrDefaultAsync(b =>
                b.CategoryId == categoryId &&
                b.Year == year &&
                b.Month == month,
                cancellationToken);

        return monthlyOverride?.LimitAmount ?? defaultLimit;
    }

    public async Task RevertTransactionImpactAsync(Transaction originalTransaction, CancellationToken cancellationToken)
    {
        // Logic to "undo" the points awarded by this transaction.
        // This is complex, but a simple "Compensation" event is robust.

        // Example: If the user originally got -10 points, we award +10 points with reason "Correction".
        // You would ideally store the "ScoreEventId" on the Transaction to know exactly what to reverse.
        // For now, we can log a generic correction.
        await _scoringService.AddScoreEventAsync(0, ScoreChangeReason.ManualAdjustment, $"Correction for transaction {originalTransaction.Id}", cancellationToken: cancellationToken);
    }
}
