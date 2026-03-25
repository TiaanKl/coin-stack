using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class DailyChallengeService : IDailyChallengeService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;
    private readonly ILevelService _levelService;

    private static readonly (string Title, string Description, string Icon, int Xp, ChallengeFrequency Freq)[] ChallengePool =
    [
        ("No-Spend Day", "Don't log any expense transactions today.", "fa-ban", 15, ChallengeFrequency.Daily),
        ("Log Every Expense", "Record at least 3 expense transactions today.", "fa-receipt", 10, ChallengeFrequency.Daily),
        ("Mindful Moment", "Complete a CBT thought record about a financial worry.", "fa-brain", 15, ChallengeFrequency.Daily),
        ("Gratitude Check", "Write down 3 things you're financially grateful for using the CBT journal.", "fa-heart", 10, ChallengeFrequency.Daily),
        ("Subscription Audit", "Review your subscriptions and check if any can be cancelled.", "fa-rotate", 20, ChallengeFrequency.Weekly),
        ("Savings Boost", "Add any amount to your savings today.", "fa-piggy-bank", 15, ChallengeFrequency.Daily),
        ("Future Self Letter", "Complete the 'Letter to Future You' CBT exercise.", "fa-envelope", 20, ChallengeFrequency.Weekly),
        ("Budget Review", "Check all your bucket allocations are on track.", "fa-chart-pie", 10, ChallengeFrequency.Daily),
        ("Impulse Resist", "Add something to the Waitlist instead of buying it immediately.", "fa-shield-halved", 15, ChallengeFrequency.Daily),
        ("Reflect & Learn", "Complete a pending reflection prompt.", "fa-lightbulb", 10, ChallengeFrequency.Daily),
        ("Values Check", "Complete the Values Alignment Check exercise.", "fa-compass", 20, ChallengeFrequency.Weekly),
        ("Spending Trigger Map", "Identify a spending trigger using the Trigger Map exercise.", "fa-map", 15, ChallengeFrequency.Weekly),
        ("Week in Review", "Look at your weekly recap and identify one improvement area.", "fa-calendar-week", 15, ChallengeFrequency.Weekly),
        ("Debt Progress", "Make or log a debt payment.", "fa-money-bill-trend-up", 15, ChallengeFrequency.Daily),
        ("Goal Contribution", "Contribute any amount towards a savings goal.", "fa-bullseye", 10, ChallengeFrequency.Daily),
    ];

    public DailyChallengeService(IDbContextFactory<CoinStackDbContext> dbFactory, ILevelService levelService)
    {
        _dbFactory = dbFactory;
        _levelService = levelService;
    }

    public async Task<List<DailyChallenge>> GetTodaysChallengesAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var today = DateTime.UtcNow.Date;
        var challenges = await db.DailyChallenges
            .AsNoTracking()
            .Where(c => c.AssignedDateUtc >= today && c.AssignedDateUtc < today.AddDays(1))
            .OrderBy(c => c.Status)
            .ThenBy(c => c.CreatedAtUtc)
            .ToListAsync(ct);

        if (challenges.Count == 0)
        {
            await GenerateDailyChallengesAsync(ct);
            challenges = await db.DailyChallenges
                .AsNoTracking()
                .Where(c => c.AssignedDateUtc >= today && c.AssignedDateUtc < today.AddDays(1))
                .OrderBy(c => c.Status)
                .ToListAsync(ct);
        }

        return challenges;
    }

    public async Task<DailyChallenge?> CompleteChallengeAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var challenge = await db.DailyChallenges.FindAsync([id], ct);
        if (challenge is null || challenge.Status != ChallengeStatus.Active) return null;

        challenge.Status = ChallengeStatus.Completed;
        challenge.CompletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await _levelService.AddXpAsync(challenge.XpReward, ct);

        return challenge;
    }

    public async Task GenerateDailyChallengesAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var today = DateTime.UtcNow.Date;
        var existing = await db.DailyChallenges
            .AnyAsync(c => c.AssignedDateUtc >= today && c.AssignedDateUtc < today.AddDays(1), ct);

        if (existing) return;

        var random = new Random(today.DayOfYear + today.Year);
        var dailyPool = ChallengePool.Where(c => c.Freq == ChallengeFrequency.Daily).ToArray();
        var weeklyPool = ChallengePool.Where(c => c.Freq == ChallengeFrequency.Weekly).ToArray();

        var selected = new List<(string Title, string Description, string Icon, int Xp, ChallengeFrequency Freq)>();

        // Pick 3 daily challenges
        var dailyIndices = Enumerable.Range(0, dailyPool.Length).OrderBy(_ => random.Next()).Take(3);
        selected.AddRange(dailyIndices.Select(i => dailyPool[i]));

        // On Mondays, add a weekly challenge
        if (today.DayOfWeek == DayOfWeek.Monday && weeklyPool.Length > 0)
        {
            var weekly = weeklyPool[random.Next(weeklyPool.Length)];
            selected.Add(weekly);
        }

        var challenges = selected.Select(s => new DailyChallenge
        {
            Title = s.Title,
            Description = s.Description,
            Icon = s.Icon,
            XpReward = s.Xp,
            Frequency = s.Freq,
            Status = ChallengeStatus.Active,
            AssignedDateUtc = today,
            ExpiresAtUtc = s.Freq == ChallengeFrequency.Weekly ? today.AddDays(7) : today.AddDays(1),
        });

        db.DailyChallenges.AddRange(challenges);
        await db.SaveChangesAsync(ct);
    }

    public async Task ExpireOldChallengesAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var expired = await db.DailyChallenges
            .Where(c => c.Status == ChallengeStatus.Active && c.ExpiresAtUtc < now)
            .ToListAsync(ct);

        foreach (var c in expired)
            c.Status = ChallengeStatus.Expired;

        if (expired.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    public async Task<int> GetCompletedCountTodayAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var today = DateTime.UtcNow.Date;
        return await db.DailyChallenges
            .CountAsync(c => c.Status == ChallengeStatus.Completed && c.CompletedAtUtc >= today, ct);
    }

    public async Task<int> GetCompletedCountThisWeekAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
        return await db.DailyChallenges
            .CountAsync(c => c.Status == ChallengeStatus.Completed && c.CompletedAtUtc >= weekStart, ct);
    }
}
