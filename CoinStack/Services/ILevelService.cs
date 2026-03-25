using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface ILevelService
{
    Task<UserLevel> GetCurrentLevelAsync(CancellationToken ct = default);
    Task<UserLevel> AddXpAsync(int xpAmount, CancellationToken ct = default);
    int GetXpRequiredForLevel(int level);
    string GetTitleForLevel(int level);
    Task<bool> CheckLevelUpAsync(CancellationToken ct = default);
}
