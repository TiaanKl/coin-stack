using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoinStack.Mobile.Services;

/// <summary>
/// The mobile pages' view of the app. Every rule lives in the CoinStack.Core services the web and desktop
/// apps use; this class only shapes their results for the mobile screens. It keeps one service scope for
/// the app's lifetime, the same way a web session keeps one per visitor.
/// </summary>
public sealed class MobileFinanceService : IMobileFinanceService, IDisposable
{
    private readonly IServiceScope _scope;

    public MobileFinanceService(IServiceScopeFactory scopeFactory)
    {
        _scope = scopeFactory.CreateScope();
    }

    private T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public void Dispose() => _scope.Dispose();

    /// <summary>Opening the dashboard: the day's catch-up first (if the app stayed open past midnight), then the check-in.</summary>
    public async Task ProcessDailyCheckInAsync(CancellationToken cancellationToken = default)
    {
        await Get<IDailyCatchUpService>().RunIfDueAsync(cancellationToken);
        await Get<IGameLoopService>().ProcessDailyCheckInAsync(cancellationToken);
    }

    public async Task<MobileDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var transactions = Get<ITransactionService>();
        var (totalIncome, totalExpense) = await transactions.GetLifetimeTotalsAsync(cancellationToken);
        var recent = await transactions.GetRecentAsync(10, cancellationToken);
        var buckets = await Get<IBucketService>().GetAllAsync(cancellationToken);
        var state = await Get<IGameLoopService>().GetCurrentStateAsync(cancellationToken);
        var dailyStreak = state.Streaks.FirstOrDefault(s => s.Type == StreakType.DailyCheckIn)?.CurrentCount ?? 0;
        var subscriptions = await Get<ISubscriptionService>().GetAllAsync(cancellationToken);
        var debts = await Get<IDebtService>().GetAllAsync(cancellationToken);

        await using var db = await Get<IDbContextFactory<CoinStackDbContext>>().CreateDbContextAsync(cancellationToken);
        var pendingReflections = await db.Reflections.CountAsync(r => !r.IsCompleted, cancellationToken);

        return new MobileDashboardSnapshot(
            totalIncome,
            totalExpense,
            totalIncome - totalExpense,
            state.TotalScore,
            dailyStreak,
            subscriptions.Count(s => s.Status == SubscriptionStatus.Active),
            debts.Count(d => d.CurrentBalance > 0),
            pendingReflections,
            recent,
            buckets);
    }

    public async Task<IReadOnlyList<Transaction>> GetTransactionsAsync(CancellationToken cancellationToken = default)
        => await Get<ITransactionService>().GetAllAsync(cancellationToken);

    public async Task AddTransactionAsync(decimal amount, TransactionType type, string description, int? bucketId = null, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("Amount must be greater than 0.");
        }

        await Get<ITransactionService>().CreateWithGameLoopAsync(new Transaction
        {
            Amount = amount,
            Type = type,
            Description = description?.Trim() ?? string.Empty,
            BucketId = bucketId,
            OccurredAtUtc = DateTime.UtcNow,
            ExpenseKind = type == TransactionType.Expense ? ExpenseKind.Discretionary : ExpenseKind.Mandatory
        }, 0, cancellationToken);
    }

    public async Task<IReadOnlyList<Bucket>> GetBucketsAsync(CancellationToken cancellationToken = default)
        => await Get<IBucketService>().GetAllAsync(cancellationToken);

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

        var buckets = Get<IBucketService>();
        var existing = await buckets.GetAllAsync(cancellationToken);
        await buckets.CreateAsync(new Bucket
        {
            Name = trimmedName,
            AllocatedAmount = allocatedAmount,
            IsSavings = isSavings,
            SortOrder = existing.Count == 0 ? 1 : existing.Max(b => b.SortOrder) + 1
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<Goal>> GetGoalsAsync(CancellationToken cancellationToken = default)
        => (await Get<IGoalService>().GetAllAsync(cancellationToken)).OrderBy(g => g.Name).ToList();

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

        // The starting amount is money saved elsewhere; adding to the goal later moves it out of savings.
        var current = Math.Min(currentAmount, targetAmount);
        await Get<IGoalService>().CreateAsync(new Goal
        {
            Name = trimmedName,
            TargetAmount = targetAmount,
            CurrentAmount = current,
            TargetDateUtc = targetDateUtc?.Date,
            Status = current >= targetAmount ? GoalStatus.Completed : GoalStatus.Active
        }, cancellationToken);
    }

    public Task<GoalMoneyResult> AddGoalContributionAsync(int goalId, decimal amount, CancellationToken cancellationToken = default)
        => Get<IGoalService>().ContributeAsync(goalId, amount, cancellationToken);

    public Task DeleteGoalAsync(int goalId, CancellationToken cancellationToken = default)
        => Get<IGoalService>().DeleteAsync(goalId, cancellationToken);

    public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        => Get<ISettingsService>().GetAsync(cancellationToken);

    public async Task<IReadOnlyList<Subscription>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
        => (await Get<ISubscriptionService>().GetAllAsync(cancellationToken)).OrderBy(s => s.Name).ToList();

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

        await Get<ISubscriptionService>().CreateAsync(new Subscription
        {
            Name = trimmedName,
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
            Cost = cost,
            Cycle = cycle,
            Status = status,
            DebitOrderDay = debitOrderDay
        }, cancellationToken);
    }

    public Task DeleteSubscriptionAsync(int id, CancellationToken cancellationToken = default)
        => Get<ISubscriptionService>().DeleteAsync(id, cancellationToken);

    public async Task<IReadOnlyList<MobileDebtSummary>> GetDebtsAsync(CancellationToken cancellationToken = default)
    {
        return (await Get<IDebtService>().GetAllAsync(cancellationToken))
            .OrderBy(d => d.Name)
            .Select(MapDebtSummary)
            .ToList();
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

        await Get<IDebtService>().CreateAsync(new DebtAccount
        {
            Name = trimmedName,
            Provider = string.IsNullOrWhiteSpace(provider) ? null : provider.Trim(),
            TotalAmount = totalAmount,
            CurrentBalance = currentBalance,
            MonthlyPaymentAmount = monthlyPayment,
            InterestRatePercent = Math.Max(0, interestRatePercent),
            PaymentStartDateUtc = (paymentStartDateUtc ?? DateTime.UtcNow).Date,
            PlannedTermMonths = plannedTermMonths
        }, cancellationToken);
    }

    public Task RecordDebtPaymentAsync(int debtId, decimal paymentAmount, CancellationToken cancellationToken = default)
    {
        if (paymentAmount <= 0)
        {
            throw new InvalidOperationException("Payment amount must be greater than 0.");
        }

        return Get<IDebtService>().RecordPaymentAsync(debtId, paymentAmount, cancellationToken);
    }

    public Task DeleteDebtAsync(int debtId, CancellationToken cancellationToken = default)
        => Get<IDebtService>().DeleteAsync(debtId, cancellationToken);

    public Task<Reflection?> GetPendingReflectionAsync(CancellationToken cancellationToken = default)
        => Get<IReflectionService>().GetPendingAsync(cancellationToken);

    public async Task<IReadOnlyList<Reflection>> GetRecentReflectionsAsync(int count = 20, CancellationToken cancellationToken = default)
        => await Get<IReflectionService>().GetRecentAsync(Math.Max(1, count), cancellationToken);

    public async Task<int> CreateManualReflectionAsync(CancellationToken cancellationToken = default)
        => (await Get<IReflectionService>().CreateReflectionAsync(ReflectionTrigger.ManualEntry, cancellationToken: cancellationToken)).Id;

    public Task CompleteReflectionAsync(int reflectionId, string response, int moodBefore, int moodAfter, EmotionTag? emotionTag = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new InvalidOperationException("Reflection response is required.");
        }

        return Get<IReflectionService>().CompleteReflectionAsync(
            reflectionId, response.Trim(), Math.Clamp(moodBefore, 1, 10), Math.Clamp(moodAfter, 1, 10), emotionTag, cancellationToken);
    }

    public async Task<MobileSavingsSnapshot> GetSavingsSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var savings = Get<ISavingsService>();
        var state = await savings.GetStateAsync(cancellationToken);
        var summaries = (await savings.GetMonthlySummariesAsync(cancellationToken)).OrderByDescending(s => s.Month).Take(24).ToList();
        var fallbackEvents = (await savings.GetFallbackEventsAsync(cancellationToken)).OrderByDescending(e => e.OccurredAtUtc).Take(30).ToList();
        var fallbackUsedThisMonth = await savings.GetFallbackUsedThisMonthAsync(cancellationToken);
        var projections = (await savings.GetProjectionsAsync(12, includeInterest: true, cancellationToken))
            .Select(p => new MobileSavingsProjectionPoint(p.Month, p.Projected))
            .ToList();

        return new MobileSavingsSnapshot(state, summaries, fallbackEvents, fallbackUsedThisMonth, projections);
    }

    public async Task<bool> CalculateSavingsForCurrentMonthAsync(CancellationToken cancellationToken = default)
        => await Get<ISavingsService>().CalculateMonthAsync(cancellationToken) is not null;

    public Task<decimal> WithdrawSavingsAsync(decimal amount, string reason, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("Withdrawal amount must be greater than 0.");
        }

        return Get<ISavingsService>().WithdrawForEmergencyAsync(
            amount, string.IsNullOrWhiteSpace(reason) ? "Manual withdrawal" : reason.Trim(), cancellationToken: cancellationToken);
    }

    public Task SetSavingsFallbackEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        => Get<ISavingsService>().SetFallbackEnabledAsync(enabled, cancellationToken);

    /// <summary>The same figures as the Reports page, for the same pay period.</summary>
    public async Task<MobileReportSnapshot> GetReportSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var settings = await Get<ISettingsService>().GetAsync(cancellationToken);
        var transactions = Get<ITransactionService>();
        var clock = Get<TimeProvider>();
        var period = BudgetPeriod.Containing(settings.MonthStartDay, clock.GetUtcNow().UtcDateTime, clock.LocalTimeZone);

        var (periodIncome, periodExpense) = await transactions.GetIncomeExpenseForPeriodAsync(period.StartUtc, period.EndUtc, cancellationToken);

        var savings = Get<ISavingsService>();
        var savingsState = await savings.GetStateAsync(cancellationToken);
        var fallbackUsedThisMonth = await savings.GetFallbackUsedThisMonthAsync(cancellationToken);

        var openDebts = (await Get<IDebtService>().GetAllAsync(cancellationToken)).Where(d => d.CurrentBalance > 0).ToList();

        var buckets = await Get<IBucketService>().GetAllAsync(cancellationToken);
        var spentByBucket = (await transactions.GetBucketSpendingForPeriodAsync(period.StartUtc, period.EndUtc, cancellationToken))
            .ToDictionary(x => x.BucketId, x => x.Spent);
        var topBucketSpending = buckets
            .Select(b => new MobileBucketSpendRow(b.Name, b.AllocatedAmount, spentByBucket.GetValueOrDefault(b.Id)))
            .Where(x => x.Spent > 0)
            .OrderByDescending(x => x.Spent)
            .Take(6)
            .ToList();

        var today = clock.Today();
        var dailyStart = today.AddDays(-13);
        var dailyByDate = (await transactions.GetDailyNetForRangeAsync(clock.StartOfDayUtc(dailyStart), clock.StartOfDayUtc(today.AddDays(1)), cancellationToken))
            .ToDictionary(x => x.Date.Date);
        var dailyNet = Enumerable.Range(0, 14)
            .Select(i => dailyStart.AddDays(i))
            .Select(day => dailyByDate.TryGetValue(day, out var row)
                ? new MobileDailyNetRow(day, row.Income, row.Expense)
                : new MobileDailyNetRow(day, 0m, 0m))
            .Reverse()
            .ToList();

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

        settings.Currency = settings.Currency.Trim().ToUpperInvariant();
        settings.MonthStartDay = BudgetPeriod.NormalizePayday(settings.MonthStartDay);
        await Get<ISettingsService>().SaveAsync(settings, cancellationToken);
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

    // ── Categories ──

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => (await Get<ICategoryService>().GetAllAsync(cancellationToken)).OrderBy(c => c.Name).ToList();

    public async Task AddCategoryAsync(string name, CategoryScope scope, string? colorHex = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Category name is required.");

        await Get<ICategoryService>().CreateAsync(new Category
        {
            Name = name.Trim(),
            Scope = scope,
            ColorHex = colorHex?.Trim()
        }, cancellationToken);
    }

    public Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
        => Get<ICategoryService>().DeleteAsync(id, cancellationToken);

    // ── Income Snapshot ──

    /// <summary>Income for the current pay period, as on the web Income and Reports pages.</summary>
    public async Task<MobileIncomeSnapshot> GetIncomeSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var settings = await Get<ISettingsService>().GetAsync(cancellationToken);
        var income = (await Get<ITransactionService>().GetAllAsync(cancellationToken))
            .Where(t => t.Type == TransactionType.Income)
            .ToList();

        var clock = Get<TimeProvider>();
        var today = clock.Today();
        var period = BudgetPeriod.Containing(settings.MonthStartDay, clock.GetUtcNow().UtcDateTime, clock.LocalTimeZone);
        var periodToDate = income.Where(t => period.Contains(t.OccurredAtUtc)).Sum(t => t.Amount);
        var yearToDate = income.Where(t => clock.LocalDate(t.OccurredAtUtc).Year == today.Year).Sum(t => t.Amount);

        return new MobileIncomeSnapshot(period, periodToDate, yearToDate, yearToDate / today.Month, income.Take(20).ToList());
    }

    // ── Waitlist ──

    public async Task<IReadOnlyList<WaitlistItem>> GetWaitlistItemsAsync(CancellationToken cancellationToken = default)
    {
        var waitlist = Get<IWaitlistService>();
        await waitlist.EvaluateCoolOffsAsync(cancellationToken);
        return (await waitlist.GetAllAsync(cancellationToken)).OrderByDescending(w => w.CreatedAtUtc).ToList();
    }

    public async Task AddWaitlistItemAsync(string name, decimal estimatedCost, string? description = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Item name is required.");

        await Get<IWaitlistService>().CreateAsync(new WaitlistItem
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            EstimatedCost = estimatedCost
        }, cancellationToken);
    }

    public Task DeleteWaitlistItemAsync(int id, CancellationToken cancellationToken = default)
        => Get<IWaitlistService>().DeleteAsync(id, cancellationToken);

    /// <summary>Records the purchase as an expense, like the web Waitlist page.</summary>
    public Task MarkWaitlistItemPurchasedAsync(int id, CancellationToken cancellationToken = default)
        => Get<IWaitlistService>().MarkPurchasedAsync(id, cancellationToken: cancellationToken);

    // ── Achievements ──

    public async Task<IReadOnlyList<Achievement>> GetAchievementsAsync(CancellationToken cancellationToken = default)
    {
        var achievements = Get<IAchievementService>();
        await achievements.EvaluateAsync(cancellationToken);
        return (await achievements.GetAllAsync(cancellationToken)).OrderBy(a => a.Category).ThenBy(a => a.Title).ToList();
    }

    public async Task<MobileLevelInfo> GetLevelInfoAsync(CancellationToken cancellationToken = default)
    {
        var levels = Get<ILevelService>();
        var level = await levels.GetCurrentLevelAsync(cancellationToken);
        var totalScore = await Get<IScoringService>().GetTotalScoreAsync(cancellationToken);

        return new MobileLevelInfo(level.Level, level.Title, level.CurrentXp, levels.GetXpRequiredForLevel(level.Level), totalScore);
    }

    // ── Challenges ──

    public async Task<IReadOnlyList<DailyChallenge>> GetTodaysChallengesAsync(CancellationToken cancellationToken = default)
        => (await Get<IDailyChallengeService>().GetTodaysChallengesAsync(cancellationToken)).OrderBy(c => c.Status).ToList();

    public Task CompleteChallengeAsync(int id, CancellationToken cancellationToken = default)
        => Get<IDailyChallengeService>().CompleteChallengeAsync(id, cancellationToken);

    public Task<int> GetChallengesCompletedTodayAsync(CancellationToken cancellationToken = default)
        => Get<IDailyChallengeService>().GetCompletedCountTodayAsync(cancellationToken);

    public Task<int> GetChallengesCompletedThisWeekAsync(CancellationToken cancellationToken = default)
        => Get<IDailyChallengeService>().GetCompletedCountThisWeekAsync(cancellationToken);

    // ── CBT Journal ──

    public async Task<IReadOnlyList<CbtJournalEntry>> GetCbtEntriesAsync(int count = 20, CancellationToken cancellationToken = default)
        => await Get<ICbtService>().GetRecentEntriesAsync(count, cancellationToken);

    public async Task AddCbtEntryAsync(string situation, string automaticThought, string emotion, int emotionIntensity, string rationalResponse, int moodBefore, int moodAfter, CognitiveDistortion? distortion = null, CancellationToken cancellationToken = default)
    {
        await Get<ICbtService>().CreateEntryAsync(new CbtJournalEntry
        {
            Situation = situation.Trim(),
            AutomaticThought = automaticThought.Trim(),
            Emotion = emotion.Trim(),
            EmotionIntensity = emotionIntensity,
            RationalResponse = rationalResponse.Trim(),
            MoodBefore = moodBefore,
            MoodAfter = moodAfter,
            Distortion = distortion
        }, cancellationToken);
    }

    // ── Weekly Recap ──

    public async Task<IReadOnlyList<WeeklyRecap>> GetWeeklyRecapsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        return (await Get<IWeeklyRecapService>().GetAllAsync(cancellationToken))
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.WeekNumber)
            .Take(count)
            .ToList();
    }

    public async Task<IReadOnlyList<ScoreEvent>> GetRecentScoreEventsAsync(int count = 20, CancellationToken cancellationToken = default)
        => await Get<IScoringService>().GetRecentEventsAsync(count, cancellationToken);

    // ── Backup & restore ──

    public string BackupFileName => Get<IBackupService>().SuggestedFileName;

    public Task<byte[]> CreateBackupAsync(string password, CancellationToken cancellationToken = default)
        => Get<IBackupService>().CreateAsync(password, cancellationToken);

    public Task<BackupManifest> ReadBackupAsync(byte[] backup, string password, CancellationToken cancellationToken = default)
        => Get<IBackupService>().ReadAsync(backup, password, cancellationToken);

    public Task<string?> RestoreBackupAsync(byte[] backup, string password, CancellationToken cancellationToken = default)
        => Get<IBackupService>().RestoreAsync(backup, password, cancellationToken);
}
