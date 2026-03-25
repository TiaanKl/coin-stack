using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface IDailyChallengeService
{
    Task<List<DailyChallenge>> GetTodaysChallengesAsync(CancellationToken ct = default);
    Task<DailyChallenge?> CompleteChallengeAsync(int id, CancellationToken ct = default);
    Task GenerateDailyChallengesAsync(CancellationToken ct = default);
    Task ExpireOldChallengesAsync(CancellationToken ct = default);
    Task<int> GetCompletedCountTodayAsync(CancellationToken ct = default);
    Task<int> GetCompletedCountThisWeekAsync(CancellationToken ct = default);
}
