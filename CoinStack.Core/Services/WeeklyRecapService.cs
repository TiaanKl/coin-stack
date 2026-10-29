using System.Globalization;
using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class WeeklyRecapService : IWeeklyRecapService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    public WeeklyRecapService(IDbContextFactory<CoinStackDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<WeeklyRecap?> GetCurrentWeekRecapAsync(CancellationToken ct = default)
    {
        var (week, year) = GetCurrentWeekAndYear();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.WeeklyRecaps
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.WeekNumber == week && r.Year == year, ct);
    }

    public async Task<WeeklyRecap?> GetLatestUnviewedAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.WeeklyRecaps
            .AsNoTracking()
            .Where(r => !r.IsViewed)
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.WeekNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<WeeklyRecap>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.WeeklyRecaps
            .AsNoTracking()
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.WeekNumber)
            .ToListAsync(ct);
    }

    public async Task<WeeklyRecap> GenerateRecapAsync(CancellationToken ct = default)
    {
        var (week, year) = GetPreviousWeekAndYear();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await GenerateRecapAsync(db, year, week, ct);
    }

    public async Task<int> GenerateMissingRecapsAsync(int maxWeeks = 12, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var firstActivity = await db.Transactions.AsNoTracking()
            .OrderBy(t => t.OccurredAtUtc)
            .Select(t => (DateTime?)t.OccurredAtUtc)
            .FirstOrDefaultAsync(ct);
        var firstScore = await db.ScoreEvents.AsNoTracking()
            .OrderBy(s => s.CreatedAtUtc)
            .Select(s => (DateTime?)s.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        var earliest = new[] { firstActivity, firstScore }.Where(d => d.HasValue).Min();
        if (earliest is null)
        {
            return 0;
        }

        var (lastWeek, lastYear) = GetPreviousWeekAndYear();
        var lastWeekStart = FirstDateOfWeek(lastYear, lastWeek);
        var oldestAllowed = lastWeekStart.AddDays(-7 * (Math.Max(1, maxWeeks) - 1));
        var start = FirstDateOfWeek(ISOWeek.GetYear(earliest.Value), ISOWeek.GetWeekOfYear(earliest.Value));
        if (start < oldestAllowed)
        {
            start = oldestAllowed;
        }

        var existing = (await db.WeeklyRecaps.AsNoTracking()
                .Select(r => new { r.Year, r.WeekNumber })
                .ToListAsync(ct))
            .Select(r => (r.Year, r.WeekNumber))
            .ToHashSet();

        var created = 0;
        for (var weekStart = start; weekStart <= lastWeekStart; weekStart = weekStart.AddDays(7))
        {
            var year = ISOWeek.GetYear(weekStart);
            var week = ISOWeek.GetWeekOfYear(weekStart);
            if (existing.Contains((year, week)))
            {
                continue;
            }

            await GenerateRecapAsync(db, year, week, ct);
            created++;
        }

        return created;
    }

    private static async Task<WeeklyRecap> GenerateRecapAsync(CoinStackDbContext db, int year, int week, CancellationToken ct)
    {
        var existing = await db.WeeklyRecaps
            .FirstOrDefaultAsync(r => r.WeekNumber == week && r.Year == year, ct);

        if (existing is not null) return existing;

        var weekStart = FirstDateOfWeek(year, week);
        var weekEnd = weekStart.AddDays(7);

        // Category is needed for the top-category figure; without it every week showed "None".
        var transactions = await db.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.OccurredAtUtc >= weekStart && t.OccurredAtUtc < weekEnd)
            .ToListAsync(ct);

        var spent = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
        var income = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var saved = income - spent;

        var topCategory = transactions
            .Where(t => t.Type == TransactionType.Expense && t.Category != null)
            .GroupBy(t => t.Category!.Name)
            .OrderByDescending(g => g.Sum(t => t.Amount))
            .Select(g => g.Key)
            .FirstOrDefault() ?? "None";

        var challenges = await db.DailyChallenges
            .CountAsync(c => c.Status == ChallengeStatus.Completed
                && c.CompletedAtUtc >= weekStart && c.CompletedAtUtc < weekEnd, ct);

        var reflections = await db.Reflections
            .CountAsync(r => r.CreatedAtUtc >= weekStart && r.CreatedAtUtc < weekEnd, ct);

        // Days checked in during this week. The streak rows only hold today's count (of whichever
        // streak type changed last), which says nothing about a past week.
        var checkInTimes = await db.ScoreEvents
            .AsNoTracking()
            .Where(s => s.Reason == ScoreChangeReason.DailyCheckIn && s.CreatedAtUtc >= weekStart && s.CreatedAtUtc < weekEnd)
            .Select(s => s.CreatedAtUtc)
            .ToListAsync(ct);
        var streak = checkInTimes.Select(t => t.Date).Distinct().Count();

        var points = await db.ScoreEvents
            .Where(s => s.CreatedAtUtc >= weekStart && s.CreatedAtUtc < weekEnd)
            .SumAsync(s => s.Points, ct);

        var insight = GenerateInsight(spent, income, saved, topCategory, challenges, streak);

        var recap = new WeeklyRecap
        {
            WeekNumber = week,
            Year = year,
            TotalSpent = spent,
            TotalIncome = income,
            TotalSaved = saved,
            PointsEarned = points,
            ChallengesCompleted = challenges,
            ReflectionsCompleted = reflections,
            StreakDays = streak,
            TopCategory = topCategory,
            InsightMessage = insight,
            IsViewed = false,
        };

        db.WeeklyRecaps.Add(recap);
        await db.SaveChangesAsync(ct);
        return recap;
    }

    public async Task MarkViewedAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var recap = await db.WeeklyRecaps.FindAsync([id], ct);
        if (recap is null) return;
        recap.IsViewed = true;
        await db.SaveChangesAsync(ct);
    }

    private static string GenerateInsight(decimal spent, decimal income, decimal saved, string topCategory, int challenges, int streak)
    {
        var parts = new List<string>();

        if (saved > 0)
            parts.Add($"You saved {saved:C0} this week — nice work!");
        else if (saved < 0)
            parts.Add($"You spent {Math.Abs(saved):C0} more than you earned this week. Let's reset next week.");
        else
            parts.Add("You broke even this week — every little bit counts.");

        if (topCategory != "None")
            parts.Add($"Most of your spending went to {topCategory}.");

        if (challenges >= 3)
            parts.Add($"Great hustle — you completed {challenges} challenges!");
        else if (challenges > 0)
            parts.Add($"You completed {challenges} challenge{(challenges > 1 ? "s" : "")}. Try for more next week!");

        if (streak >= 7)
            parts.Add("You checked in every day this week. Keep it going!");

        return string.Join(" ", parts);
    }

    private static (int Week, int Year) GetCurrentWeekAndYear()
    {
        var today = DateTime.UtcNow;
        var week = ISOWeek.GetWeekOfYear(today);
        var year = ISOWeek.GetYear(today);
        return (week, year);
    }

    private static (int Week, int Year) GetPreviousWeekAndYear()
    {
        var lastWeek = DateTime.UtcNow.AddDays(-7);
        var week = ISOWeek.GetWeekOfYear(lastWeek);
        var year = ISOWeek.GetYear(lastWeek);
        return (week, year);
    }

    private static DateTime FirstDateOfWeek(int year, int week)
        => ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
}
