using CoinStack.Data.Entities;

namespace CoinStack.Mobile.Services;

public interface IMobileFinanceService
{
    Task ProcessDailyCheckInAsync(CancellationToken cancellationToken = default);
    Task<MobileDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetTransactionsAsync(CancellationToken cancellationToken = default);
    Task AddTransactionAsync(decimal amount, TransactionType type, string description, int? bucketId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Bucket>> GetBucketsAsync(CancellationToken cancellationToken = default);
    Task AddBucketAsync(string name, decimal allocatedAmount, bool isSavings, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Goal>> GetGoalsAsync(CancellationToken cancellationToken = default);
    Task AddGoalAsync(string name, decimal targetAmount, decimal currentAmount = 0, DateTime? targetDateUtc = null, CancellationToken cancellationToken = default);
    Task AddGoalContributionAsync(int goalId, decimal amount, CancellationToken cancellationToken = default);
    Task DeleteGoalAsync(int goalId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Subscription>> GetSubscriptionsAsync(CancellationToken cancellationToken = default);
    Task AddSubscriptionAsync(string name, string category, decimal cost, SubscriptionCycle cycle, SubscriptionStatus status, int? debitOrderDay = null, CancellationToken cancellationToken = default);
    Task DeleteSubscriptionAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MobileDebtSummary>> GetDebtsAsync(CancellationToken cancellationToken = default);
    Task AddDebtAsync(string name, string? provider, decimal totalAmount, decimal currentBalance, decimal monthlyPayment, decimal interestRatePercent, DateTime? paymentStartDateUtc = null, int? plannedTermMonths = null, CancellationToken cancellationToken = default);
    Task RecordDebtPaymentAsync(int debtId, decimal paymentAmount, CancellationToken cancellationToken = default);
    Task DeleteDebtAsync(int debtId, CancellationToken cancellationToken = default);

    Task<Reflection?> GetPendingReflectionAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Reflection>> GetRecentReflectionsAsync(int count = 20, CancellationToken cancellationToken = default);
    Task<int> CreateManualReflectionAsync(CancellationToken cancellationToken = default);
    Task CompleteReflectionAsync(int reflectionId, string response, int moodBefore, int moodAfter, EmotionTag? emotionTag = null, CancellationToken cancellationToken = default);

    Task<MobileSavingsSnapshot> GetSavingsSnapshotAsync(CancellationToken cancellationToken = default);
    Task<bool> CalculateSavingsForCurrentMonthAsync(CancellationToken cancellationToken = default);
    Task<decimal> WithdrawSavingsAsync(decimal amount, string reason, CancellationToken cancellationToken = default);
    Task SetSavingsFallbackEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    Task<MobileReportSnapshot> GetReportSnapshotAsync(CancellationToken cancellationToken = default);

    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public sealed record MobileDashboardSnapshot(
    decimal TotalIncome,
    decimal TotalExpense,
    decimal NetSaved,
    int TotalScore,
    int DailyCheckInStreak,
    int ActiveSubscriptions,
    int ActiveDebts,
    int PendingReflections,
    IReadOnlyList<Transaction> RecentTransactions,
    IReadOnlyList<Bucket> Buckets);

public sealed record MobileDebtSummary(
    int Id,
    string Name,
    string? Provider,
    decimal TotalAmount,
    decimal CurrentBalance,
    decimal MonthlyPaymentAmount,
    decimal InterestRatePercent,
    DateTime PaymentStartDateUtc,
    int? PlannedTermMonths,
    int? EstimatedPayoffMonths,
    DateTime? EstimatedPayoffDateUtc);

public sealed record MobileSavingsSnapshot(
    SavingsState State,
    IReadOnlyList<SavingsMonthlySummary> MonthlySummaries,
    IReadOnlyList<SavingsFallbackEvent> FallbackEvents,
    decimal FallbackUsedThisMonth,
    IReadOnlyList<MobileSavingsProjectionPoint> Projections);

public sealed record MobileSavingsProjectionPoint(string Month, decimal Projected);

public sealed record MobileReportSnapshot(
    decimal PeriodIncome,
    decimal PeriodExpense,
    decimal PeriodNet,
    decimal SavingsAvailable,
    decimal FallbackUsedThisMonth,
    int OpenDebtCount,
    decimal TotalDebtOutstanding,
    decimal TotalDebtMonthlyPayment,
    IReadOnlyList<MobileBucketSpendRow> TopBucketSpending,
    IReadOnlyList<MobileDailyNetRow> DailyNet14Days);

public sealed record MobileBucketSpendRow(string Name, decimal Allocated, decimal Spent);
public sealed record MobileDailyNetRow(DateTime Date, decimal Income, decimal Expense)
{
    public decimal Net => Income - Expense;
}
