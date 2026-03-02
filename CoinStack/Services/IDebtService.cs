using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface IDebtService
{
    Task<List<DebtAccount>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DebtAccount?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DebtAccount> CreateAsync(DebtAccount debt, CancellationToken cancellationToken = default);
    Task UpdateAsync(DebtAccount debt, CancellationToken cancellationToken = default);
    Task RecordPaymentAsync(int debtId, decimal amount, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}