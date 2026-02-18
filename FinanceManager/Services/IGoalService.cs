using FinanceManager.Data.Entities;

namespace FinanceManager.Services;

public interface IGoalService
{
    Task<List<Goal>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Goal?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Goal> CreateAsync(Goal goal, CancellationToken cancellationToken = default);
    Task UpdateAsync(Goal goal, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
