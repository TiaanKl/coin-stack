using CoinStack.Data.Entities;

namespace CoinStack.Services;

public sealed record BucketSpendSummary(int BucketId, decimal Spent);

public sealed record DailyNetSummary(DateTime Date, decimal Income, decimal Expense)
{
    public decimal Net => Income - Expense;
}

public interface ITransactionService
{
    Task<List<Transaction>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<Transaction>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
    Task<List<Transaction>> GetFromDateAsync(DateTime fromUtc, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Transaction> CreateAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<(Transaction Transaction, GameTransactionResult Result)> CreateWithGameLoopAsync(
        Transaction transaction,
        int userTimezoneOffsetHours,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<decimal> GetExpenseTotalForBudgetPeriodAsync(int monthStartDay, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<(decimal TotalIncome, decimal TotalExpense)> GetLifetimeTotalsAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetNetBalanceBeforeAsync(DateTime beforeUtc, CancellationToken cancellationToken = default);
    Task<(decimal Income, decimal Expense)> GetIncomeExpenseForPeriodAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default);
    Task<List<BucketSpendSummary>> GetBucketSpendingForPeriodAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default);
    Task<List<DailyNetSummary>> GetDailyNetForRangeAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default);

    Task ApplyAutoDeductionsForBudgetPeriodAsync(int monthStartDay, DateTime utcNow, CancellationToken cancellationToken = default);
}
