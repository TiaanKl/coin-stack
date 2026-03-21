using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class GameLoopService : IGameLoopService
{
    private const int DailyCheckInPoints = 2;
    private const string DailyCheckInDescription = "Daily check-in";

    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;
    private readonly IBucketService _bucketService;
    private readonly IScoringService _scoringService;
    private readonly IReflectionService _reflectionService;
    private readonly ISettingsService _settingsService;

    public GameLoopService(
        IDbContextFactory<CoinStackDbContext> dbFactory,
        IBucketService bucketService,
        IScoringService scoringService,
        IReflectionService reflectionService,
        ISettingsService settingsService)
    {
        _dbFactory = dbFactory;
        _bucketService = bucketService;
        _scoringService = scoringService;
        _reflectionService = reflectionService;
        _settingsService = settingsService;
    }

    public async Task<GameState> GetCurrentStateAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var settings = await _settingsService.GetAsync(cancellationToken);
        var buckets = await _bucketService.GetAllAsync(cancellationToken);
        var spent = await _bucketService.GetSpentAmountsForPeriodAsync(settings.MonthStartDay, now, cancellationToken);
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
        var now = DateTime.UtcNow;
        var settings = await _settingsService.GetAsync(cancellationToken);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var streak = await db.Streaks.FirstOrDefaultAsync(x => x.Type == StreakType.DailyCheckIn, cancellationToken);

        if (streak is null)
        {
            if (settings.EnableStreaks)
            {
                streak = new Streak { Type = StreakType.DailyCheckIn, CurrentCount = 1, BestCount = 1, LastIncrementedAtUtc = now };
                db.Streaks.Add(streak);
                await db.SaveChangesAsync(cancellationToken);
            }
            if (settings.EnableScoring)
            {
                await _scoringService.AddScoreEventAsync(2, ScoreChangeReason.DailyCheckIn, "Daily check-in!", cancellationToken: cancellationToken);
            }
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
        if (settings.EnableStreaks)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        if (settings.EnableScoring)
        {
            await _scoringService.AddScoreEventAsync(DailyCheckInPoints, ScoreChangeReason.DailyCheckIn, DailyCheckInDescription, cancellationToken: cancellationToken);
        }

        if (settings.EnableStreaks && settings.EnableScoring && streak.CurrentCount > 0 && streak.CurrentCount % 7 == 0)
        {
            var weeks = streak.CurrentCount / 7;
            var points = weeks;

            await _scoringService.AddScoreEventAsync(
                points,
                ScoreChangeReason.StreakBonus,
                $"{weeks}-week streak bonus!",
                cancellationToken: cancellationToken);
        }

        if (settings.EnableStreaks)
        {
            var buckets = await _bucketService.GetAllAsync(cancellationToken);
            var spent = await _bucketService.GetSpentAmountsForPeriodAsync(settings.MonthStartDay, now, cancellationToken);
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
    }

    public async Task<GameTransactionResult> ProcessTransactionAsync(
    Transaction transaction,
    int userTimezoneOffsetHours,
    CancellationToken cancellationToken = default)
    {
        var result = new GameTransactionResult();

        if (transaction.BucketId is null)
        {
            return result;
        }

        var settings = await _settingsService.GetAsync(cancellationToken);

        var bucket = await _bucketService.GetByIdAsync(transaction.BucketId.Value, cancellationToken);
        if (bucket is null) return result;

        if (transaction.Type == TransactionType.Income && bucket.IsSavings && bucket.GoalId.HasValue)
        {
            await using var goalDb = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var linkedGoal = await goalDb.Goals.FirstOrDefaultAsync(g => g.Id == bucket.GoalId.Value, cancellationToken);
            if (linkedGoal != null)
            {
                linkedGoal.CurrentAmount += transaction.Amount;
                if (linkedGoal.CurrentAmount >= linkedGoal.TargetAmount && linkedGoal.Status != GoalStatus.Completed)
                {
                    linkedGoal.Status = GoalStatus.Completed;
                    if (settings.EnableScoring)
                    {
                        await _scoringService.AddScoreEventAsync(50, ScoreChangeReason.GoalAchieved, $"Goal '{linkedGoal.Name}' Reached!", cancellationToken: cancellationToken);
                        result.PointsChanged = 50;
                        result.Kind = FeedbackKind.GoalAchieved;
                        result.Message = $"Goal '{linkedGoal.Name}' Reached! +50 pts";
                    }
                }
                await goalDb.SaveChangesAsync(cancellationToken);
            }
        }

        if (transaction.Type != TransactionType.Expense)
        {
            return result;
        }

        var transactionDate = transaction.OccurredAtUtc.AddHours(userTimezoneOffsetHours);
        var currentYear = transactionDate.Year;
        var currentMonth = transactionDate.Month;

        var monthlyLimit = await GetMonthlyLimitAsync(
            transaction.CategoryId,
            transaction.BucketId,
            currentYear,
            currentMonth,
            bucket.AllocatedAmount,
            cancellationToken);

        var spentDict = await _bucketService.GetSpentAmountsForPeriodAsync(settings.MonthStartDay, transaction.OccurredAtUtc.AddHours(userTimezoneOffsetHours), cancellationToken);
        var totalSpentIncludingCurrent = spentDict.GetValueOrDefault(bucket.Id);

        var spentBefore = totalSpentIncludingCurrent - transaction.Amount;
        if (spentBefore < 0) spentBefore = 0;

        if (settings.EnableScoring)
        {
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
        }

        var spentAfter = spentBefore + transaction.Amount;

        if (settings.EnableReflections)
        {
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
            else if (monthlyLimit > 0 && transaction.Amount >= monthlyLimit * (settings.LargeExpenseThreshold / 100m))
            {
                trigger = ReflectionTrigger.LargeExpense;
            }

            if (trigger.HasValue)
            {
                var reflection = await _reflectionService.CreateReflectionAsync(trigger.Value, transaction.Id, cancellationToken);
                result.TriggeredReflection = true;
                result.Reflection = reflection;
            }
        }

        // Preserve any explicit feedback decided earlier in the pipeline.
        if (result.Kind == FeedbackKind.Normal)
        {
            result.Kind = result.PointsChanged switch
            {
                > 0 => FeedbackKind.Positive,
                < 0 when bucket.IsSavings => FeedbackKind.SavingsDip,
                < 0 => FeedbackKind.Negative,
                _ => FeedbackKind.Normal,
            };
        }

        if (string.IsNullOrWhiteSpace(result.Message))
        {
            result.Message = result.PointsChanged switch
            {
                > 0 => $"+{result.PointsChanged} points! Keeping it on track!",
                < 0 => $"{result.PointsChanged} points. Watch that budget.",
                _ => "Transaction recorded."
            };
        }

        return result;
    }

    private async Task<decimal> GetMonthlyLimitAsync(
        int? categoryId,
        int? bucketId,
        int year,
        int month,
        decimal defaultLimit,
        CancellationToken cancellationToken)
    {
        if (categoryId is null && bucketId is null)
            return defaultLimit;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var monthlyOverride = await db.Budgets
            .AsNoTracking()
            .FirstOrDefaultAsync(b =>
                (bucketId != null ? b.BucketId == bucketId : b.CategoryId == categoryId) &&
                b.Year == year &&
                b.Month == month,
                cancellationToken);

        return monthlyOverride?.LimitAmount ?? defaultLimit;
    }

    public async Task RevertTransactionImpactAsync(Transaction originalTransaction, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var events = await db.ScoreEvents
            .Where(e => e.TransactionId == originalTransaction.Id)
            .ToListAsync(cancellationToken);

        if (events.Count > 0)
        {
            db.ScoreEvents.RemoveRange(events);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
