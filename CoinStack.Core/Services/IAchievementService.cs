using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface IAchievementService
{
    Task<List<Achievement>> GetAllAsync(CancellationToken ct = default);
    Task<List<Achievement>> GetUnlockedAsync(CancellationToken ct = default);
    Task<Achievement?> TryUnlockAsync(string key, CancellationToken ct = default);
    Task SeedAchievementsAsync(CancellationToken ct = default);
    Task<int> GetUnlockedCountAsync(CancellationToken ct = default);
    /// <summary>Unlocks achievements whose conditions are now met, awards their XP, and returns the new ones.</summary>
    Task<List<Achievement>> EvaluateAsync(CancellationToken ct = default);
}
