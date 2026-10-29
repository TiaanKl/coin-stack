using System.Globalization;
using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

public sealed class GamificationTests
{
    [Fact]
    public async Task Evaluate_Unlocks_Earned_Achievements_And_Awards_Their_Xp_Once()
    {
        var (connection, options) = await CreateDatabaseAsync();
        await using (connection)
        {
            var factory = new TestDbContextFactory(options);
            var levels = new LevelService(factory);
            var achievements = new AchievementService(factory, levels);
            await achievements.SeedAchievementsAsync();

            await using (var db = new CoinStackDbContext(options))
            {
                db.Transactions.Add(new Transaction { Description = "Coffee", Amount = 30m, Type = TransactionType.Expense });
                await db.SaveChangesAsync();
            }

            var unlocked = await achievements.EvaluateAsync();
            var again = await achievements.EvaluateAsync();

            var first = Assert.Single(unlocked);
            Assert.Equal("first-transaction", first.Key);
            Assert.Empty(again);
            Assert.Equal(first.XpReward, (await levels.GetCurrentLevelAsync()).TotalXp);
        }
    }

    [Fact]
    public async Task Missing_Recaps_Are_Generated_For_Every_Finished_Week()
    {
        var (connection, options) = await CreateDatabaseAsync();
        await using (connection)
        {
            var lastWeekMonday = MondayOf(DateTime.UtcNow.Date).AddDays(-7);
            var threeWeeksAgo = lastWeekMonday.AddDays(-14);

            await using (var db = new CoinStackDbContext(options))
            {
                var dining = new Category { Name = "Dining", Scope = CategoryScope.Expense };
                var groceries = new Category { Name = "Groceries", Scope = CategoryScope.Expense };
                db.Categories.AddRange(dining, groceries);
                await db.SaveChangesAsync();

                db.Transactions.AddRange(
                    new Transaction { Description = "Old", Amount = 10m, Type = TransactionType.Expense, OccurredAtUtc = threeWeeksAgo.AddDays(1) },
                    new Transaction { Description = "Dinner", Amount = 400m, Type = TransactionType.Expense, CategoryId = dining.Id, OccurredAtUtc = lastWeekMonday.AddDays(2) },
                    new Transaction { Description = "Shop", Amount = 150m, Type = TransactionType.Expense, CategoryId = groceries.Id, OccurredAtUtc = lastWeekMonday.AddDays(3) });

                var checkIns = Enumerable.Range(0, 3)
                    .Select(_ => new ScoreEvent { Points = 2, Reason = ScoreChangeReason.DailyCheckIn, Description = "Daily check-in" })
                    .ToList();
                db.ScoreEvents.AddRange(checkIns);
                await db.SaveChangesAsync();

                // Saving stamps CreatedAtUtc with the current time, so backdate the check-ins afterwards.
                checkIns[0].CreatedAtUtc = lastWeekMonday.AddHours(8);
                checkIns[1].CreatedAtUtc = lastWeekMonday.AddDays(1).AddHours(8);
                checkIns[2].CreatedAtUtc = lastWeekMonday.AddDays(1).AddHours(20);
                await db.SaveChangesAsync();
            }

            var service = new WeeklyRecapService(new TestDbContextFactory(options));
            var created = await service.GenerateMissingRecapsAsync();
            var createdAgain = await service.GenerateMissingRecapsAsync();

            Assert.Equal(3, created);
            Assert.Equal(0, createdAgain);

            var recaps = await service.GetAllAsync();
            var lastWeek = recaps.Single(r => r.WeekNumber == ISOWeek.GetWeekOfYear(lastWeekMonday) && r.Year == ISOWeek.GetYear(lastWeekMonday));
            Assert.Equal("Dining", lastWeek.TopCategory);
            Assert.Equal(2, lastWeek.StreakDays);
            Assert.Equal(550m, lastWeek.TotalSpent);
        }
    }

    [Fact]
    public async Task This_Weeks_Weekly_Challenge_Stays_Listed_All_Week()
    {
        var (connection, options) = await CreateDatabaseAsync();
        await using (connection)
        {
            var monday = MondayOf(DateTime.UtcNow.Date);

            await using (var db = new CoinStackDbContext(options))
            {
                db.DailyChallenges.AddRange(
                    new DailyChallenge { Title = "This week", Frequency = ChallengeFrequency.Weekly, Status = ChallengeStatus.Active, AssignedDateUtc = monday, ExpiresAtUtc = monday.AddDays(7) },
                    new DailyChallenge { Title = "Last week", Frequency = ChallengeFrequency.Weekly, Status = ChallengeStatus.Active, AssignedDateUtc = monday.AddDays(-7), ExpiresAtUtc = monday });
                await db.SaveChangesAsync();
            }

            var service = new DailyChallengeService(new TestDbContextFactory(options), new FakeLevelService());
            var challenges = await service.GetTodaysChallengesAsync();

            var weekly = Assert.Single(challenges, c => c.Frequency == ChallengeFrequency.Weekly);
            Assert.Equal("This week", weekly.Title);
            Assert.Equal(3, challenges.Count(c => c.Frequency == ChallengeFrequency.Daily));
        }
    }

    [Fact]
    public async Task A_Weekly_Challenge_Is_Added_Whatever_Day_The_App_Is_Opened()
    {
        var (connection, options) = await CreateDatabaseAsync();
        await using (connection)
        {
            var service = new DailyChallengeService(new TestDbContextFactory(options), new FakeLevelService());
            var challenges = await service.GetTodaysChallengesAsync();

            var weekly = Assert.Single(challenges, c => c.Frequency == ChallengeFrequency.Weekly);
            // Monday on the device's calendar, stored as the UTC time that day starts.
            Assert.Equal(TimeProvider.System.StartOfDayUtc(MondayOf(TimeProvider.System.Today())), weekly.AssignedDateUtc);
        }
    }

    private static DateTime MondayOf(DateTime date)
    {
        return date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    }

    private static async Task<(SqliteConnection Connection, DbContextOptions<CoinStackDbContext> Options)> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CoinStackDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new CoinStackDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return (connection, options);
    }
}
