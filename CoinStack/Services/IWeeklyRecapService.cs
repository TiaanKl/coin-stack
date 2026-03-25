using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface IWeeklyRecapService
{
    Task<WeeklyRecap?> GetCurrentWeekRecapAsync(CancellationToken ct = default);
    Task<WeeklyRecap?> GetLatestUnviewedAsync(CancellationToken ct = default);
    Task<List<WeeklyRecap>> GetAllAsync(CancellationToken ct = default);
    Task<WeeklyRecap> GenerateRecapAsync(CancellationToken ct = default);
    Task MarkViewedAsync(int id, CancellationToken ct = default);
}
