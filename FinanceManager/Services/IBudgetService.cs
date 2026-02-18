using FinanceManager.Data.Entities;

namespace FinanceManager.Services;

public interface IBudgetService
{
    Task<List<Budget>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Budget?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Budget> CreateAsync(Budget budget, CancellationToken cancellationToken = default);
    Task UpdateAsync(Budget budget, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
