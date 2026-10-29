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

        var service = new WaitlistService(factory, settings, new FakeScoringService(), new FakeTransactionService());

        await using var db = new CoinStackDbContext(options);
        var itemId = await db.WaitlistItems.Select(x => x.Id).FirstAsync();

        var score = await service.CalculateReadinessScoreAsync(itemId);

        // The over-budget bucket scores 0 of 15 for budget; counted outside the period it would score 15 (60).
        Assert.Equal(45, score);
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

    [Fact]
    public void Readiness_Depends_On_The_Price()
    {
        var cheap = WaitlistReadiness.Calculate(Inputs(cost: 50m));
        var expensive = WaitlistReadiness.Calculate(Inputs(cost: 50000m));

        Assert.True(cheap > expensive, $"R50 scored {cheap}, R50 000 scored {expensive}.");
        Assert.True(cheap >= 70, $"A R50 item on a R20 000 income should be affordable, scored {cheap}.");
    }

    [Fact]
    public void Item_Costing_More_Than_Income_And_Savings_Is_Never_Ready()
    {
        var score = WaitlistReadiness.Calculate(Inputs(cost: 50000m, savingsAvailable: 10000m));

        Assert.True(score <= WaitlistReadiness.UnaffordableCap, $"Scored {score}.");
    }

    [Fact]
    public void Savings_On_Hand_Make_A_Big_Purchase_More_Affordable()
    {
        var withoutSavings = WaitlistReadiness.Calculate(Inputs(cost: 15000m));
        var withSavings = WaitlistReadiness.Calculate(Inputs(cost: 15000m, savingsAvailable: 200000m));

        Assert.True(withSavings > withoutSavings);
    }

    private static WaitlistReadinessInputs Inputs(decimal cost, decimal savingsAvailable = 0m) => new(
        ItemCost: cost,
        BucketCount: 4,
        BucketsOverBudget: 0,
        SavingsRate: 1m,
        MonthlyIncome: 20000m,
        MonthlyDebtPayments: 0m,
        DebtCount: 0,
        SavingsAvailable: savingsAvailable,
        Emotion: EmotionTag.Neutral,
        Priority: WaitlistPriority.Medium,
        DaysWaited: 14);

    [Fact]
    public async Task Buying_After_The_Cool_Off_Is_Not_An_Impulse_Buy()
    {
        var (connection, options) = await CreateDatabaseAsync();
        await using (connection)
        {
            var itemId = await AddItemAsync(options, coolOffUntil: DateTime.UtcNow.AddMinutes(-1));
            var transactions = new FakeTransactionService();
            var service = CreateService(options, transactions);

            var (item, _) = await service.MarkPurchasedAsync(itemId);

            Assert.NotNull(item);
            Assert.False(transactions.LastCreated!.IsImpulse);
        }
    }

    [Fact]
    public async Task Buying_During_The_Cool_Off_Is_An_Impulse_Buy()
    {
        var (connection, options) = await CreateDatabaseAsync();
        await using (connection)
        {
            var itemId = await AddItemAsync(options, coolOffUntil: DateTime.UtcNow.AddDays(3));
            var transactions = new FakeTransactionService();

            await CreateService(options, transactions).MarkPurchasedAsync(itemId);

            Assert.True(transactions.LastCreated!.IsImpulse);
        }
    }

    [Fact]
    public async Task Didnt_Buy_Keeps_The_Item_For_The_Saved_By_Waiting_Total()
    {
        var (connection, options) = await CreateDatabaseAsync();
        await using (connection)
        {
            var itemId = await AddItemAsync(options, coolOffUntil: DateTime.UtcNow.AddMinutes(-1), cost: 1500m);
            await AddItemAsync(options, coolOffUntil: DateTime.UtcNow.AddDays(1), cost: 99m);
            var service = CreateService(options, new FakeTransactionService());

            await service.MarkResistedAsync(itemId);
            await service.MarkResistedAsync(itemId);

            var saved = await service.GetSavedByWaitingAsync();
            Assert.Equal(1500m, saved.Total);
            Assert.Equal(1, saved.ItemCount);
            Assert.DoesNotContain(await service.GetAllAsync(), i => i.Id == itemId);
            Assert.NotNull(await service.GetByIdAsync(itemId));
        }
    }

    private static WaitlistService CreateService(DbContextOptions<CoinStackDbContext> options, FakeTransactionService transactions)
        => new(new TestDbContextFactory(options), new FakeSettingsService(new AppSettings()), new FakeScoringService(), transactions);

    private static async Task<int> AddItemAsync(DbContextOptions<CoinStackDbContext> options, DateTime coolOffUntil, decimal cost = 200m)
    {
        await using var db = new CoinStackDbContext(options);
        var item = new WaitlistItem { Name = "Headphones", EstimatedCost = cost, CoolOffUntil = coolOffUntil };
        db.WaitlistItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
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
