using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Mobile.Services;

public sealed class MobileFinanceService : IMobileFinanceService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;
    private const int DailyCheckInPoints = 2;

    public MobileFinanceService(IDbContextFactory<CoinStackDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task ProcessDailyCheckInAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var settings = await db.AppSettings.FirstOrDefaultAsync(cancellationToken) ?? new AppSettings();
        var streak = await db.Streaks.FirstOrDefaultAsync(x => x.Type == StreakType.DailyCheckIn, cancellationToken);

        if (streak is not null && streak.LastIncrementedAtUtc.Date == now.Date)
        {
            return;
        }

        if (streak is null)
        {
            streak = new Streak
            {
                Type = StreakType.DailyCheckIn,
                CurrentCount = 1,
                BestCount = 1,
                LastIncrementedAtUtc = now
            };

            db.Streaks.Add(streak);
        }
        else
        {
            var daysSince = (now.Date - streak.LastIncrementedAtUtc.Date).Days;
            streak.CurrentCount = daysSince == 1 ? streak.CurrentCount + 1 : 1;
            streak.BestCount = Math.Max(streak.BestCount, streak.CurrentCount);
            streak.LastIncrementedAtUtc = now;
        }

        if (settings.EnableScoring)
        {
            db.ScoreEvents.Add(new ScoreEvent
            {
                Points = DailyCheckInPoints,
                Reason = ScoreChangeReason.DailyCheckIn,
                Description = "Daily check-in"
            });

            if (streak.CurrentCount > 0 && streak.CurrentCount % 7 == 0)
            {
                var weeks = streak.CurrentCount / 7;
                db.ScoreEvents.Add(new ScoreEvent
                {
                    Points = weeks,
                    Reason = ScoreChangeReason.StreakBonus,
                    Description = $"{weeks}-week streak bonus"
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MobileDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var transactions = await db.Transactions
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        var totalIncome = await db.Transactions
            .Where(x => x.Type == TransactionType.Income)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var totalExpense = await db.Transactions
            .Where(x => x.Type == TransactionType.Expense)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var buckets = await db.Buckets
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var totalScore = await db.ScoreEvents
            .AsNoTracking()
            .SumAsync(x => (int?)x.Points, cancellationToken) ?? 0;

        var dailyStreak = await db.Streaks
            .AsNoTracking()
            .Where(x => x.Type == StreakType.DailyCheckIn)
            .Select(x => (int?)x.CurrentCount)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        var activeSubscriptions = await db.Subscriptions
            .AsNoTracking()
            .CountAsync(x => x.Status == SubscriptionStatus.Active, cancellationToken);

        var activeDebts = await db.DebtAccounts
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var pendingReflections = await db.Reflections
            .AsNoTracking()
            .CountAsync(x => !x.IsCompleted, cancellationToken);

        return new MobileDashboardSnapshot(
            totalIncome,
            totalExpense,
            totalIncome - totalExpense,
            totalScore,
            dailyStreak,
            activeSubscriptions,
            activeDebts,
            pendingReflections,
            transactions,
            buckets);
    }

    public async Task<IReadOnlyList<Transaction>> GetTransactionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Transactions
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddTransactionAsync(decimal amount, TransactionType type, string description, int? bucketId = null, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("Amount must be greater than 0.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        db.Transactions.Add(new Transaction
        {
            Amount = amount,
            Type = type,
            Description = description?.Trim() ?? string.Empty,
            BucketId = bucketId,
            OccurredAtUtc = DateTime.UtcNow,
            ExpenseKind = type == TransactionType.Expense ? ExpenseKind.Discretionary : ExpenseKind.Mandatory
        });

        var settings = await db.AppSettings.FirstOrDefaultAsync(cancellationToken) ?? new AppSettings();
        if (settings.EnableScoring && type == TransactionType.Expense)
        {
            db.ScoreEvents.Add(new ScoreEvent
            {
                Points = -1,
                Reason = ScoreChangeReason.ManualAdjustment,
                Description = "Expense recorded",
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Bucket>> GetBucketsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Buckets
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddBucketAsync(string name, decimal allocatedAmount, bool isSavings, CancellationToken cancellationToken = default)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new InvalidOperationException("Bucket name is required.");
        }

        if (allocatedAmount < 0)
        {
            throw new InvalidOperationException("Allocated amount cannot be negative.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var nextOrder = await db.Buckets.AnyAsync(cancellationToken)
            ? await db.Buckets.MaxAsync(x => x.SortOrder, cancellationToken) + 1
            : 1;

        db.Buckets.Add(new Bucket
        {
            Name = trimmedName,
            AllocatedAmount = allocatedAmount,
            IsSavings = isSavings,
            SortOrder = nextOrder
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Goal>> GetGoalsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Goals
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddGoalAsync(string name, decimal targetAmount, decimal currentAmount = 0, DateTime? targetDateUtc = null, CancellationToken cancellationToken = default)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new InvalidOperationException("Goal name is required.");
        }

        if (targetAmount <= 0)
        {
            throw new InvalidOperationException("Goal target amount must be greater than 0.");
        }

        if (currentAmount < 0)
        {
            throw new InvalidOperationException("Goal current amount cannot be negative.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var current = Math.Min(currentAmount, targetAmount);
        db.Goals.Add(new Goal
        {
            Name = trimmedName,
            TargetAmount = targetAmount,
            CurrentAmount = current,
            TargetDateUtc = targetDateUtc?.Date,
            Status = current >= targetAmount ? GoalStatus.Completed : GoalStatus.Active
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddGoalContributionAsync(int goalId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("Contribution amount must be greater than 0.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var goal = await db.Goals.FirstOrDefaultAsync(x => x.Id == goalId, cancellationToken);
        if (goal is null)
        {
            return;
        }

        var wasCompleted = goal.Status == GoalStatus.Completed;
        goal.CurrentAmount = Math.Min(goal.TargetAmount, goal.CurrentAmount + amount);
        goal.Status = goal.CurrentAmount >= goal.TargetAmount ? GoalStatus.Completed : GoalStatus.Active;

        var settings = await db.AppSettings.FirstOrDefaultAsync(cancellationToken) ?? new AppSettings();
        if (settings.EnableScoring && !wasCompleted && goal.Status == GoalStatus.Completed)
        {
            db.ScoreEvents.Add(new ScoreEvent
            {
                Points = 50,
                Reason = ScoreChangeReason.GoalAchieved,
                Description = $"Goal '{goal.Name}' reached"
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGoalAsync(int goalId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var goal = await db.Goals.FirstOrDefaultAsync(x => x.Id == goalId, cancellationToken);
        if (goal is null)
        {
            return;
        }

        db.Goals.Remove(goal);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var settings = await db.AppSettings
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return settings ?? new AppSettings();
    }

    public async Task<IReadOnlyList<Subscription>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Subscriptions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddSubscriptionAsync(string name, string category, decimal cost, SubscriptionCycle cycle, SubscriptionStatus status, int? debitOrderDay = null, CancellationToken cancellationToken = default)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new InvalidOperationException("Subscription name is required.");
        }

        if (cost < 0)
        {
            throw new InvalidOperationException("Subscription cost cannot be negative.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.Subscriptions.Add(new Subscription
        {
            Name = trimmedName,
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
            Cost = cost,
            Cycle = cycle,
            Status = status,
            DebitOrderDay = debitOrderDay
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteSubscriptionAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Subscriptions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        db.Subscriptions.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MobileDebtSummary>> GetDebtsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var debts = await db.DebtAccounts
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return debts.Select(MapDebtSummary).ToList();
    }

    public async Task AddDebtAsync(string name, string? provider, decimal totalAmount, decimal currentBalance, decimal monthlyPayment, decimal interestRatePercent, DateTime? paymentStartDateUtc = null, int? plannedTermMonths = null, CancellationToken cancellationToken = default)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new InvalidOperationException("Debt name is required.");
        }

        if (totalAmount <= 0 || currentBalance < 0 || monthlyPayment <= 0)
        {
            throw new InvalidOperationException("Provide valid debt amounts.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.DebtAccounts.Add(new DebtAccount
        {
            Name = trimmedName,
            Provider = string.IsNullOrWhiteSpace(provider) ? null : provider.Trim(),
            TotalAmount = totalAmount,
            CurrentBalance = Math.Min(currentBalance, totalAmount),
            MonthlyPaymentAmount = monthlyPayment,
            InterestRatePercent = Math.Max(0, interestRatePercent),
            PaymentStartDateUtc = (paymentStartDateUtc ?? DateTime.UtcNow).Date,
            PlannedTermMonths = plannedTermMonths
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordDebtPaymentAsync(int debtId, decimal paymentAmount, CancellationToken cancellationToken = default)
    {
        if (paymentAmount <= 0)
        {
            throw new InvalidOperationException("Payment amount must be greater than 0.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var debt = await db.DebtAccounts.FirstOrDefaultAsync(x => x.Id == debtId, cancellationToken);
        if (debt is null)
        {
            return;
        }

        debt.CurrentBalance = Math.Max(0m, debt.CurrentBalance - paymentAmount);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteDebtAsync(int debtId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.DebtAccounts.FirstOrDefaultAsync(x => x.Id == debtId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        db.DebtAccounts.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Reflection?> GetPendingReflectionAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Reflections
            .AsNoTracking()
            .Where(x => !x.IsCompleted)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Reflection>> GetRecentReflectionsAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Reflections
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Max(1, count))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CreateManualReflectionAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var reflection = new Reflection
        {
            Trigger = ReflectionTrigger.ManualEntry,
            Prompt = "Take a moment to check in — how are you feeling about your finances today?"
        };

        db.Reflections.Add(reflection);
        await db.SaveChangesAsync(cancellationToken);
        return reflection.Id;
    }

    public async Task CompleteReflectionAsync(int reflectionId, string response, int moodBefore, int moodAfter, EmotionTag? emotionTag = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new InvalidOperationException("Reflection response is required.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Reflections.FirstOrDefaultAsync(x => x.Id == reflectionId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.Response = response.Trim();
        existing.MoodBefore = Math.Clamp(moodBefore, 1, 10);
        existing.MoodAfter = Math.Clamp(moodAfter, 1, 10);
        existing.EmotionTag = emotionTag;
        existing.IsCompleted = true;

        var settings = await db.AppSettings.FirstOrDefaultAsync(cancellationToken) ?? new AppSettings();
        if (settings.EnableScoring)
        {
            db.ScoreEvents.Add(new ScoreEvent
            {
                Points = 3,
                Reason = ScoreChangeReason.ReflectionCompleted,
                Description = "Completed a reflection"
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MobileSavingsSnapshot> GetSavingsSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var state = await db.SavingsState.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? new SavingsState();

        var summaries = await db.SavingsMonthlySummaries
            .AsNoTracking()
            .OrderByDescending(x => x.Month)
            .Take(24)
            .ToListAsync(cancellationToken);

        var fallbackEvents = await db.SavingsFallbackEvents
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(30)
            .ToListAsync(cancellationToken);

        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var fallbackUsedThisMonth = await db.SavingsFallbackEvents
            .AsNoTracking()
            .Where(x => x.OccurredAtUtc >= startOfMonth)
            .SumAsync(x => (decimal?)x.AmountUsed, cancellationToken) ?? 0m;

        var settings = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? new AppSettings();

        var projections = BuildSavingsProjections(settings, state, 12, includeInterest: true);

        return new MobileSavingsSnapshot(state, summaries, fallbackEvents, fallbackUsedThisMonth, projections);
    }

    public async Task<bool> CalculateSavingsForCurrentMonthAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var settings = await db.AppSettings.FirstOrDefaultAsync(cancellationToken) ?? new AppSettings();
        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");

        var state = await db.SavingsState.FirstOrDefaultAsync(cancellationToken);
        if (state is not null && state.LastCalculatedMonth == currentMonth)
        {
            return false;
        }

        var alreadyRecorded = await db.SavingsMonthlySummaries
            .AnyAsync(x => x.Month == currentMonth, cancellationToken);
        if (alreadyRecorded)
        {
            if (state is not null && state.LastCalculatedMonth != currentMonth)
            {
                state.LastCalculatedMonth = currentMonth;
                await db.SaveChangesAsync(cancellationToken);
            }

            return false;
        }

        var income = settings.MonthlyIncome;
        var baseSavings = settings.SavingsIsPercent
            ? income * (settings.MonthlySavingsPercent / 100m)
            : settings.MonthlySavingsAmount;

        var currentTotal = state?.Total ?? 0m;
        var interest = 0m;

        if (settings.SavingsInterestRate.HasValue && settings.SavingsInterestRate.Value > 0)
        {
            var apr = settings.SavingsInterestRate.Value;
            var monthlyRate = settings.SavingsInterestIsYearly ? apr / 12m : apr;
            interest = (currentTotal + baseSavings) * (monthlyRate / 100m);
        }

        var totalAdded = baseSavings + interest;
        var newRunningTotal = currentTotal + totalAdded;

        if (state is null)
        {
            state = new SavingsState
            {
                Total = newRunningTotal,
                Available = newRunningTotal,
                Reserved = 0,
                LastCalculatedMonth = currentMonth,
                FallbackEnabled = false
            };

            db.SavingsState.Add(state);
        }
        else
        {
            state.Total += totalAdded;
            state.Available += totalAdded;
            state.LastCalculatedMonth = currentMonth;
        }

        db.SavingsMonthlySummaries.Add(new SavingsMonthlySummary
        {
            Month = currentMonth,
            Base = baseSavings,
            Interest = interest,
            Total = totalAdded,
            RunningTotal = newRunningTotal
        });

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<decimal> WithdrawSavingsAsync(decimal amount, string reason, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("Withdrawal amount must be greater than 0.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var state = await db.SavingsState.FirstOrDefaultAsync(cancellationToken);
        if (state is null || state.Available <= 0)
        {
            return 0m;
        }

        var withdrawn = Math.Min(amount, state.Available);
        state.Available -= withdrawn;

        db.SavingsFallbackEvents.Add(new SavingsFallbackEvent
        {
            OccurredAtUtc = DateTime.UtcNow,
            AmountUsed = withdrawn,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Manual withdrawal" : reason.Trim(),
            SourceName = "Mobile Savings"
        });

        await db.SaveChangesAsync(cancellationToken);
        return withdrawn;
    }

    public async Task SetSavingsFallbackEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var state = await db.SavingsState.FirstOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            state = new SavingsState { FallbackEnabled = enabled };
            db.SavingsState.Add(state);
        }
        else
        {
            state.FallbackEnabled = enabled;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MobileReportSnapshot> GetReportSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var settings = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? new AppSettings();

        var nowUtc = DateTime.UtcNow;
        var (periodStartUtc, periodEndUtc) = GetBudgetPeriodBoundsUtc(settings.MonthStartDay, nowUtc);

        var periodIncome = await db.Transactions
            .AsNoTracking()
            .Where(t => t.OccurredAtUtc >= periodStartUtc && t.OccurredAtUtc < periodEndUtc && t.Type == TransactionType.Income)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        var periodExpense = await db.Transactions
            .AsNoTracking()
            .Where(t => t.OccurredAtUtc >= periodStartUtc && t.OccurredAtUtc < periodEndUtc && t.Type == TransactionType.Expense)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        var savingsState = await db.SavingsState.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? new SavingsState();

        var startOfMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var fallbackUsedThisMonth = await db.SavingsFallbackEvents
            .AsNoTracking()
            .Where(x => x.OccurredAtUtc >= startOfMonth)
            .SumAsync(x => (decimal?)x.AmountUsed, cancellationToken) ?? 0m;

        var openDebts = await db.DebtAccounts
            .AsNoTracking()
            .Where(d => d.CurrentBalance > 0)
            .ToListAsync(cancellationToken);

        var buckets = await db.Buckets.AsNoTracking().ToListAsync(cancellationToken);
        var bucketSpend = await db.Transactions
            .AsNoTracking()
            .Where(t => t.OccurredAtUtc >= periodStartUtc
                        && t.OccurredAtUtc < periodEndUtc
                        && t.Type == TransactionType.Expense
                        && t.BucketId.HasValue)
            .GroupBy(t => t.BucketId!.Value)
            .Select(g => new { BucketId = g.Key, Spent = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var spentByBucket = bucketSpend.ToDictionary(x => x.BucketId, x => x.Spent);
        var topBucketSpending = buckets
            .Select(b => new MobileBucketSpendRow(
                b.Name,
                b.AllocatedAmount,
                spentByBucket.TryGetValue(b.Id, out var spent) ? spent : 0m))
            .Where(x => x.Spent > 0)
            .OrderByDescending(x => x.Spent)
            .Take(6)
            .ToList();

        var dailyStart = nowUtc.Date.AddDays(-13);
        var dailyIncomeRows = await db.Transactions
            .AsNoTracking()
            .Where(t => t.OccurredAtUtc >= dailyStart && t.OccurredAtUtc < nowUtc.Date.AddDays(1) && t.Type == TransactionType.Income)
            .GroupBy(t => t.OccurredAtUtc.Date)
            .Select(g => new { Date = g.Key, Amount = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);

        var dailyExpenseRows = await db.Transactions
            .AsNoTracking()
            .Where(t => t.OccurredAtUtc >= dailyStart && t.OccurredAtUtc < nowUtc.Date.AddDays(1) && t.Type == TransactionType.Expense)
            .GroupBy(t => t.OccurredAtUtc.Date)
            .Select(g => new { Date = g.Key, Amount = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);

        var incomeByDate = dailyIncomeRows.ToDictionary(x => x.Date.Date, x => x.Amount);
        var expenseByDate = dailyExpenseRows.ToDictionary(x => x.Date.Date, x => x.Amount);

        var dailyNet = new List<MobileDailyNetRow>(14);
        for (var i = 0; i < 14; i++)
        {
            var day = dailyStart.AddDays(i).Date;
            var income = incomeByDate.TryGetValue(day, out var inAmount) ? inAmount : 0m;
            var expense = expenseByDate.TryGetValue(day, out var exAmount) ? exAmount : 0m;
            dailyNet.Add(new MobileDailyNetRow(day, income, expense));
        }

        dailyNet.Reverse();

        return new MobileReportSnapshot(
            periodIncome,
            periodExpense,
            periodIncome - periodExpense,
            savingsState.Available,
            fallbackUsedThisMonth,
            openDebts.Count,
            openDebts.Sum(d => d.CurrentBalance),
            openDebts.Sum(d => d.MonthlyPaymentAmount),
            topBucketSpending,
            dailyNet);
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.Currency))
        {
            throw new InvalidOperationException("Currency is required.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.AppSettings.FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            db.AppSettings.Add(new AppSettings
            {
                Currency = settings.Currency.Trim().ToUpperInvariant(),
                MonthStartDay = settings.MonthStartDay,
                MonthlyIncome = settings.MonthlyIncome,
                EnableReflections = settings.EnableReflections,
                EnableScoring = settings.EnableScoring,
                EnableStreaks = settings.EnableStreaks,
                EnableToast = settings.EnableToast,
                LargeExpenseThreshold = settings.LargeExpenseThreshold,
                SavingsIsPercent = settings.SavingsIsPercent,
                MonthlySavingsAmount = settings.MonthlySavingsAmount,
                MonthlySavingsPercent = settings.MonthlySavingsPercent,
                SavingsInterestRate = settings.SavingsInterestRate,
                SavingsInterestIsYearly = settings.SavingsInterestIsYearly
            });
        }
        else
        {
            existing.Currency = settings.Currency.Trim().ToUpperInvariant();
            existing.MonthStartDay = settings.MonthStartDay;
            existing.MonthlyIncome = settings.MonthlyIncome;
            existing.EnableReflections = settings.EnableReflections;
            existing.EnableScoring = settings.EnableScoring;
            existing.EnableStreaks = settings.EnableStreaks;
            existing.EnableToast = settings.EnableToast;
            existing.LargeExpenseThreshold = settings.LargeExpenseThreshold;
            existing.SavingsIsPercent = settings.SavingsIsPercent;
            existing.MonthlySavingsAmount = settings.MonthlySavingsAmount;
            existing.MonthlySavingsPercent = settings.MonthlySavingsPercent;
            existing.SavingsInterestRate = settings.SavingsInterestRate;
            existing.SavingsInterestIsYearly = settings.SavingsInterestIsYearly;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static MobileDebtSummary MapDebtSummary(DebtAccount debt)
    {
        int? estimatedPayoffMonths = null;
        DateTime? estimatedPayoffDate = null;

        if (debt.MonthlyPaymentAmount > 0)
        {
            estimatedPayoffMonths = (int)Math.Ceiling(debt.CurrentBalance / debt.MonthlyPaymentAmount);
            estimatedPayoffDate = DateTime.UtcNow.Date.AddMonths(Math.Max(0, estimatedPayoffMonths.Value));
        }

        return new MobileDebtSummary(
            debt.Id,
            debt.Name,
            debt.Provider,
            debt.TotalAmount,
            debt.CurrentBalance,
            debt.MonthlyPaymentAmount,
            debt.InterestRatePercent,
            debt.PaymentStartDateUtc,
            debt.PlannedTermMonths,
            estimatedPayoffMonths,
            estimatedPayoffDate);
    }

    private static IReadOnlyList<MobileSavingsProjectionPoint> BuildSavingsProjections(
        AppSettings settings,
        SavingsState state,
        int months,
        bool includeInterest)
    {
        var result = new List<MobileSavingsProjectionPoint>(months);

        decimal income = settings.MonthlyIncome;
        decimal baseMonthly = settings.SavingsIsPercent
            ? income * (settings.MonthlySavingsPercent / 100m)
            : settings.MonthlySavingsAmount;

        decimal apr = settings.SavingsInterestRate ?? 0m;
        decimal monthlyRate = (includeInterest && apr > 0)
            ? (settings.SavingsInterestIsYearly ? apr / 12m / 100m : apr / 100m)
            : 0m;

        var running = state.Total;
        var now = DateTime.UtcNow;

        for (var i = 1; i <= months; i++)
        {
            var projectedMonth = now.AddMonths(i);
            var label = projectedMonth.ToString("yyyy-MM");

            var interest = (running + baseMonthly) * monthlyRate;
            running += baseMonthly + interest;

            result.Add(new MobileSavingsProjectionPoint(label, Math.Round(running, 2)));
        }

        return result;
    }

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
}
