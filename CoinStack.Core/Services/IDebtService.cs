using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface IDebtService
{
    Task<List<DebtAccount>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DebtAccount?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DebtAccount> CreateAsync(DebtAccount debt, CancellationToken cancellationToken = default);
    Task UpdateAsync(DebtAccount debt, CancellationToken cancellationToken = default);
    Task RecordPaymentAsync(int debtId, decimal amount, CancellationToken cancellationToken = default);

    /// <summary>Posts any monthly interest that has come due on every debt. Safe to call repeatedly.</summary>
    Task<decimal> AccrueInterestAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}