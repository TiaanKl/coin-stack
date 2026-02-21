using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface IWaitlistService
{
    Task<List<WaitlistItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<WaitlistItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<WaitlistItem> CreateAsync(WaitlistItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(WaitlistItem item, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task MarkPurchasedAsync(int id, CancellationToken cancellationToken = default);
    Task EvaluateCoolOffsAsync(CancellationToken cancellationToken = default);
    Task<int> CalculateReadinessScoreAsync(int itemId, CancellationToken cancellationToken = default);
}
