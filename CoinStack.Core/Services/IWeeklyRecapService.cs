using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface IWeeklyRecapService
{
    Task<WeeklyRecap?> GetCurrentWeekRecapAsync(CancellationToken ct = default);
    Task<WeeklyRecap?> GetLatestUnviewedAsync(CancellationToken ct = default);
    Task<List<WeeklyRecap>> GetAllAsync(CancellationToken ct = default);
    Task<WeeklyRecap> GenerateRecapAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a recap for every finished week that has none yet, back to the first recorded activity
    /// (at most <paramref name="maxWeeks"/> weeks). Returns how many were created.
    /// </summary>
    Task<int> GenerateMissingRecapsAsync(int maxWeeks = 12, CancellationToken ct = default);
    Task MarkViewedAsync(int id, CancellationToken ct = default);
}
