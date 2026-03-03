using CoinStack.Data.Entities;

namespace CoinStack.Mobile.Services;

public interface IMobileFinanceService
{
    Task<MobileDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetTransactionsAsync(CancellationToken cancellationToken = default);
    Task AddTransactionAsync(decimal amount, TransactionType type, string description, int? bucketId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Bucket>> GetBucketsAsync(CancellationToken cancellationToken = default);
    Task AddBucketAsync(string name, decimal allocatedAmount, bool isSavings, CancellationToken cancellationToken = default);
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public sealed record MobileDashboardSnapshot(
    decimal TotalIncome,
    decimal TotalExpense,
    decimal NetSaved,
    IReadOnlyList<Transaction> RecentTransactions,
    IReadOnlyList<Bucket> Buckets);
