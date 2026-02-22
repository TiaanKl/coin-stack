using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface ITransactionService
{
    Task<List<Transaction>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Transaction> CreateAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<decimal> GetExpenseTotalForBudgetPeriodAsync(int monthStartDay, DateTime utcNow, CancellationToken cancellationToken = default);

    Task ApplyAutoDeductionsForBudgetPeriodAsync(int monthStartDay, DateTime utcNow, CancellationToken cancellationToken = default);
}
