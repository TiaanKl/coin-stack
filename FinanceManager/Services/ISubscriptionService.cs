using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface ISubscriptionService
{
    Task<List<Subscription>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Subscription?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Subscription> CreateAsync(Subscription subscription, CancellationToken cancellationToken = default);
    Task UpdateAsync(Subscription subscription, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
