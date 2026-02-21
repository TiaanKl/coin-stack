using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface IBucketService
{
    Task<List<Bucket>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Bucket?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Bucket> CreateAsync(Bucket bucket, CancellationToken cancellationToken = default);
    Task UpdateAsync(Bucket bucket, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<Dictionary<int, decimal>> GetSpentAmountsAsync(int year, int month, CancellationToken cancellationToken = default);
}
