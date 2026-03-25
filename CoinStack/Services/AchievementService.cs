using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class AchievementService : IAchievementService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;
    private readonly IScoringService _scoringService;

    public AchievementService(IDbContextFactory<CoinStackDbContext> dbFactory, IScoringService scoringService)
    {
        _dbFactory = dbFactory;
        _scoringService = scoringService;
    }

    public async Task<List<Achievement>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Achievements.AsNoTracking().OrderBy(a => a.Category).ThenBy(a => a.Key).ToListAsync(ct);
    }

    public async Task<List<Achievement>> GetUnlockedAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Achievements.AsNoTracking().Where(a => a.IsUnlocked).OrderByDescending(a => a.UnlockedAtUtc).ToListAsync(ct);
    }

    public async Task<Achievement?> TryUnlockAsync(string key, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var achievement = await db.Achievements.FirstOrDefaultAsync(a => a.Key == key, ct);
        if (achievement is null || achievement.IsUnlocked) return null;

        achievement.IsUnlocked = true;
        achievement.UnlockedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return achievement;
    }

    public async Task<int> GetUnlockedCountAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Achievements.CountAsync(a => a.IsUnlocked, ct);
    }

    public async Task EvaluateAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var totalScore = await db.ScoreEvents.SumAsync(s => s.Points, ct);
        var streaks = await db.Streaks.AsNoTracking().ToListAsync(ct);
        var checkInStreak = streaks.FirstOrDefault(s => s.Type == StreakType.DailyCheckIn);
        var underBudgetStreak = streaks.FirstOrDefault(s => s.Type == StreakType.DailyUnderBudget);
        var reflectionCount = await db.Reflections.CountAsync(r => r.IsCompleted, ct);
        var journalCount = await db.CbtJournalEntries.CountAsync(ct);
        var transactionCount = await db.Transactions.CountAsync(ct);
        var goalCount = await db.Goals.CountAsync(g => g.Status == GoalStatus.Completed, ct);
        var challengeCount = await db.DailyChallenges.CountAsync(c => c.Status == ChallengeStatus.Completed, ct);

        var checks = new (string Key, bool Condition)[]
        {
            ("first-checkin", checkInStreak is not null && checkInStreak.BestCount >= 1),
            ("streak-7", checkInStreak is not null && checkInStreak.BestCount >= 7),
            ("streak-30", checkInStreak is not null && checkInStreak.BestCount >= 30),
            ("streak-100", checkInStreak is not null && checkInStreak.BestCount >= 100),
            ("budget-master-7", underBudgetStreak is not null && underBudgetStreak.BestCount >= 7),
            ("budget-master-30", underBudgetStreak is not null && underBudgetStreak.BestCount >= 30),
            ("first-reflection", reflectionCount >= 1),
            ("reflect-10", reflectionCount >= 10),
            ("reflect-50", reflectionCount >= 50),
            ("first-journal", journalCount >= 1),
            ("journal-10", journalCount >= 10),
            ("journal-25", journalCount >= 25),
            ("score-100", totalScore >= 100),
            ("score-500", totalScore >= 500),
            ("score-1000", totalScore >= 1000),
            ("first-transaction", transactionCount >= 1),
            ("transactions-100", transactionCount >= 100),
            ("first-goal", goalCount >= 1),
            ("goals-5", goalCount >= 5),
            ("first-challenge", challengeCount >= 1),
            ("challenges-10", challengeCount >= 10),
            ("challenges-50", challengeCount >= 50),
        };

        foreach (var (key, condition) in checks)
        {
            if (!condition) continue;
            var achievement = await db.Achievements.FirstOrDefaultAsync(a => a.Key == key && !a.IsUnlocked, ct);
            if (achievement is null) continue;

            achievement.IsUnlocked = true;
            achievement.UnlockedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task SeedAchievementsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.Achievements.AnyAsync(ct)) return;

        var achievements = new Achievement[]
        {
            // Streaks
            new() { Key = "first-checkin", Title = "First Steps", Description = "Complete your first daily check-in", Icon = "fa-shoe-prints", Category = AchievementCategory.Streaks, XpReward = 10 },
            new() { Key = "streak-7", Title = "Week Warrior", Description = "Maintain a 7-day check-in streak", Icon = "fa-fire", Category = AchievementCategory.Streaks, XpReward = 25 },
            new() { Key = "streak-30", Title = "Monthly Marvel", Description = "Maintain a 30-day check-in streak", Icon = "fa-fire-flame-curved", Category = AchievementCategory.Streaks, XpReward = 100 },
            new() { Key = "streak-100", Title = "Century Club", Description = "Maintain a 100-day check-in streak", Icon = "fa-medal", Category = AchievementCategory.Streaks, XpReward = 500 },

            // Budgeting
            new() { Key = "budget-master-7", Title = "Budget Apprentice", Description = "Stay under budget for 7 consecutive days", Icon = "fa-piggy-bank", Category = AchievementCategory.Budgeting, XpReward = 30 },
            new() { Key = "budget-master-30", Title = "Budget Master", Description = "Stay under budget for 30 consecutive days", Icon = "fa-crown", Category = AchievementCategory.Budgeting, XpReward = 150 },

            // Mindfulness
            new() { Key = "first-reflection", Title = "Self-Aware", Description = "Complete your first reflection", Icon = "fa-lightbulb", Category = AchievementCategory.Mindfulness, XpReward = 10 },
            new() { Key = "reflect-10", Title = "Deep Thinker", Description = "Complete 10 reflections", Icon = "fa-brain", Category = AchievementCategory.Mindfulness, XpReward = 50 },
            new() { Key = "reflect-50", Title = "Mindfulness Master", Description = "Complete 50 reflections", Icon = "fa-spa", Category = AchievementCategory.Mindfulness, XpReward = 200 },
            new() { Key = "first-journal", Title = "Thought Explorer", Description = "Write your first CBT journal entry", Icon = "fa-pen-fancy", Category = AchievementCategory.Mindfulness, XpReward = 15 },
            new() { Key = "journal-10", Title = "Pattern Spotter", Description = "Write 10 CBT journal entries", Icon = "fa-magnifying-glass", Category = AchievementCategory.Mindfulness, XpReward = 75 },
            new() { Key = "journal-25", Title = "Cognitive Champion", Description = "Write 25 CBT journal entries", Icon = "fa-graduation-cap", Category = AchievementCategory.Mindfulness, XpReward = 200 },

            // Milestones
            new() { Key = "score-100", Title = "Rising Star", Description = "Earn 100 total points", Icon = "fa-star", Category = AchievementCategory.Milestones, XpReward = 20 },
            new() { Key = "score-500", Title = "High Flyer", Description = "Earn 500 total points", Icon = "fa-rocket", Category = AchievementCategory.Milestones, XpReward = 100 },
            new() { Key = "score-1000", Title = "Legend", Description = "Earn 1,000 total points", Icon = "fa-gem", Category = AchievementCategory.Milestones, XpReward = 300 },
            new() { Key = "first-transaction", Title = "Getting Started", Description = "Log your first transaction", Icon = "fa-receipt", Category = AchievementCategory.Milestones, XpReward = 5 },
            new() { Key = "transactions-100", Title = "Diligent Tracker", Description = "Log 100 transactions", Icon = "fa-clipboard-list", Category = AchievementCategory.Milestones, XpReward = 100 },

            // Saving
            new() { Key = "first-goal", Title = "Goal Getter", Description = "Complete your first savings goal", Icon = "fa-bullseye", Category = AchievementCategory.Saving, XpReward = 50 },
            new() { Key = "goals-5", Title = "Dream Achiever", Description = "Complete 5 savings goals", Icon = "fa-trophy", Category = AchievementCategory.Saving, XpReward = 250 },

            // Challenges
            new() { Key = "first-challenge", Title = "Challenge Accepted", Description = "Complete your first daily challenge", Icon = "fa-bolt", Category = AchievementCategory.Challenges, XpReward = 10 },
            new() { Key = "challenges-10", Title = "Challenge Streak", Description = "Complete 10 daily challenges", Icon = "fa-shield-halved", Category = AchievementCategory.Challenges, XpReward = 75 },
            new() { Key = "challenges-50", Title = "Challenge Champion", Description = "Complete 50 daily challenges", Icon = "fa-chess-king", Category = AchievementCategory.Challenges, XpReward = 300 },
        };

        db.Achievements.AddRange(achievements);
        await db.SaveChangesAsync(ct);
    }
}
