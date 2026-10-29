using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class LevelService : ILevelService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    private static readonly (int Level, string Title)[] LevelTitles =
    [
        (1, "Penny Starter"),
        (3, "Coin Collector"),
        (5, "Budget Buddy"),
        (8, "Savings Scout"),
        (10, "Money Mindful"),
        (13, "Finance Apprentice"),
        (16, "Cash Conscious"),
        (20, "Wealth Builder"),
        (25, "Portfolio Pro"),
        (30, "Investment Sage"),
        (35, "Financial Freedom Fighter"),
        (40, "Money Master"),
        (45, "Prosperity Pioneer"),
        (50, "Financial Zen Master"),
    ];

    public LevelService(IDbContextFactory<CoinStackDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<UserLevel> GetCurrentLevelAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var level = await db.UserLevels.FirstOrDefaultAsync(ct);
        if (level is not null) return level;

        level = new UserLevel { Level = 1, CurrentXp = 0, TotalXp = 0, Title = "Penny Starter" };
        db.UserLevels.Add(level);
        await db.SaveChangesAsync(ct);
        return level;
    }

    public async Task<UserLevel> AddXpAsync(int xpAmount, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var level = await db.UserLevels.FirstOrDefaultAsync(ct);
        if (level is null)
        {
            level = new UserLevel { Level = 1, CurrentXp = 0, TotalXp = 0, Title = "Penny Starter" };
            db.UserLevels.Add(level);
        }

        level.CurrentXp += xpAmount;
        level.TotalXp += xpAmount;

        while (level.CurrentXp >= GetXpRequiredForLevel(level.Level) && level.Level < 50)
        {
            level.CurrentXp -= GetXpRequiredForLevel(level.Level);
            level.Level++;
            level.Title = GetTitleForLevel(level.Level);
        }

        await db.SaveChangesAsync(ct);
        return level;
    }

    public int GetXpRequiredForLevel(int level)
    {
        // Exponential curve: each level needs more XP
        return (int)(50 * Math.Pow(1.15, level - 1));
    }

    public string GetTitleForLevel(int level)
    {
        var title = "Penny Starter";
        foreach (var (lvl, t) in LevelTitles)
        {
            if (level >= lvl) title = t;
        }
        return title;
    }

    public async Task<bool> CheckLevelUpAsync(CancellationToken ct = default)
    {
        var level = await GetCurrentLevelAsync(ct);
        return level.CurrentXp >= GetXpRequiredForLevel(level.Level) && level.Level < 50;
    }
}
