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

    private readonly IAchievementService? _achievementService;

    public DailyChallengeService(
        IDbContextFactory<CoinStackDbContext> dbFactory,
        ILevelService levelService,
        IAchievementService? achievementService = null)
    {
        _dbFactory = dbFactory;
        _levelService = levelService;
        _achievementService = achievementService;
    }

    /// <summary>Today's daily challenges plus this week's weekly challenge, which stays visible until it expires.</summary>
    public async Task<List<DailyChallenge>> GetTodaysChallengesAsync(CancellationToken ct = default)
    {
        await GenerateDailyChallengesAsync(ct);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var today = DateTime.UtcNow.Date;
        var weekStart = StartOfWeek(today);

        return await db.DailyChallenges
            .AsNoTracking()
            .Where(c => (c.AssignedDateUtc >= today && c.AssignedDateUtc < today.AddDays(1))
                        || (c.Frequency == ChallengeFrequency.Weekly
                            && c.AssignedDateUtc >= weekStart
                            && c.AssignedDateUtc < weekStart.AddDays(7)))
            .OrderBy(c => c.Status)
            .ThenBy(c => c.Frequency)
            .ThenBy(c => c.CreatedAtUtc)
            .ToListAsync(ct);
    }

    /// <summary>The Monday that starts the week containing <paramref name="date"/>.</summary>
    private static DateTime StartOfWeek(DateTime date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-daysSinceMonday);
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

        if (_achievementService is not null)
        {
            await _achievementService.EvaluateAsync(ct);
        }

        return challenge;
    }

    /// <summary>
    /// Adds today's three daily challenges, and this week's weekly challenge if the week has none yet,
    /// whichever day the app is first opened. The weekly one runs Monday to Monday.
    /// </summary>
    public async Task GenerateDailyChallengesAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var today = DateTime.UtcNow.Date;
        var weekStart = StartOfWeek(today);

        var hasDaily = await db.DailyChallenges
            .AnyAsync(c => c.Frequency == ChallengeFrequency.Daily
                           && c.AssignedDateUtc >= today && c.AssignedDateUtc < today.AddDays(1), ct);
        var hasWeekly = await db.DailyChallenges
            .AnyAsync(c => c.Frequency == ChallengeFrequency.Weekly
                           && c.AssignedDateUtc >= weekStart && c.AssignedDateUtc < weekStart.AddDays(7), ct);

        if (hasDaily && hasWeekly) return;

        var random = new Random(today.DayOfYear + today.Year);
        var dailyPool = ChallengePool.Where(c => c.Freq == ChallengeFrequency.Daily).ToArray();
        var weeklyPool = ChallengePool.Where(c => c.Freq == ChallengeFrequency.Weekly).ToArray();

        var challenges = new List<DailyChallenge>();

        if (!hasDaily)
        {
            var dailyIndices = Enumerable.Range(0, dailyPool.Length).OrderBy(_ => random.Next()).Take(3);
            challenges.AddRange(dailyIndices.Select(i => ToChallenge(dailyPool[i], today, today.AddDays(1))));
        }

        if (!hasWeekly && weeklyPool.Length > 0)
        {
            var weeklyRandom = new Random(weekStart.DayOfYear + weekStart.Year);
            challenges.Add(ToChallenge(weeklyPool[weeklyRandom.Next(weeklyPool.Length)], weekStart, weekStart.AddDays(7)));
        }

        db.DailyChallenges.AddRange(challenges);
        await db.SaveChangesAsync(ct);
    }

    private static DailyChallenge ToChallenge(
        (string Title, string Description, string Icon, int Xp, ChallengeFrequency Freq) template,
        DateTime assigned,
        DateTime expires) => new()
    {
        Title = template.Title,
        Description = template.Description,
        Icon = template.Icon,
        XpReward = template.Xp,
        Frequency = template.Freq,
        Status = ChallengeStatus.Active,
        AssignedDateUtc = assigned,
        ExpiresAtUtc = expires,
    };

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
