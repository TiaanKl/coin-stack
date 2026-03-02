using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

public sealed class WaitlistServiceTests
{
    [Fact]
    public async Task CalculateReadinessScoreAsync_Uses_Configured_Budget_Period()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CoinStackDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupDb = new CoinStackDbContext(options))
        {
            await setupDb.Database.EnsureCreatedAsync();
        }

        var now = DateTime.UtcNow;
        var monthStartDay = now.Day >= 28 ? 28 : now.Day + 1;
        var (periodStartUtc, _) = GetBudgetPeriodBoundsUtc(monthStartDay, now);

        await using (var seedDb = new CoinStackDbContext(options))
        {
            var bucket = new Bucket
            {
                Name = "Needs",
                AllocatedAmount = 100m,
                IsSavings = false,
                SortOrder = 0
            };

            var waitlistItem = new WaitlistItem
            {
                Name = "Headphones",
                EstimatedCost = 200m,
                Priority = WaitlistPriority.Medium,
                CreatedAtUtc = now
            };

            seedDb.Buckets.Add(bucket);
            seedDb.WaitlistItems.Add(waitlistItem);
            await seedDb.SaveChangesAsync();

            seedDb.Transactions.Add(new Transaction
            {
                Description = "Period expense",
                Type = TransactionType.Expense,
                ExpenseKind = ExpenseKind.Discretionary,
                Amount = 120m,
                BucketId = bucket.Id,
                OccurredAtUtc = periodStartUtc.AddDays(1)
            });

            await seedDb.SaveChangesAsync();
        }

        var factory = new TestDbContextFactory(options);
        var settings = new FakeSettingsService(new AppSettings
        {
            MonthStartDay = monthStartDay,
            MonthlyIncome = 0m
        });

        var service = new WaitlistService(factory, settings);

        await using var db = new CoinStackDbContext(options);
        var itemId = await db.WaitlistItems.Select(x => x.Id).FirstAsync();

        var score = await service.CalculateReadinessScoreAsync(itemId);

        Assert.Equal(48, score);
    }

    private static (DateTime StartUtc, DateTime EndUtc) GetBudgetPeriodBoundsUtc(int monthStartDay, DateTime utcNow)
    {
        if (monthStartDay is < 1 or > 28)
        {
            monthStartDay = 1;
        }

        var startThisMonth = new DateTime(utcNow.Year, utcNow.Month, monthStartDay, 0, 0, 0, DateTimeKind.Utc);
        var startUtc = utcNow.Day >= monthStartDay ? startThisMonth : startThisMonth.AddMonths(-1);
        var endUtc = startUtc.AddMonths(1);
        return (startUtc, endUtc);
    }
}
