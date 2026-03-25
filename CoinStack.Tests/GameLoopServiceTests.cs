using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

public sealed class GameLoopServiceTests
{
    [Fact]
    public async Task ProcessTransactionAsync_WhenScoringDisabled_DoesNotEvaluateOrAddScoreEvents()
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

        var factory = new TestDbContextFactory(options);
        var fakeBucketService = new FakeBucketService();
        fakeBucketService.Add(new Bucket { Id = 10, Name = "Essentials", AllocatedAmount = 500m, IsSavings = false });
        fakeBucketService.SpentAmountsForPeriod[10] = 0m;

        var fakeScoring = new FakeScoringService();
        var fakeReflection = new FakeReflectionService();
        var savingsService = new SavingsService(factory);
        var settings = new FakeSettingsService(new AppSettings
        {
            EnableScoring = false,
            EnableReflections = false,
            EnableStreaks = false,
            LargeExpenseThreshold = 50,
            MonthStartDay = 1,
        });

        var gameLoop = new GameLoopService(factory, fakeBucketService, fakeScoring, fakeReflection, settings, savingsService, new FakeLevelService());

        var tx = new Transaction
        {
            Id = 42,
            BucketId = 10,
            Type = TransactionType.Expense,
            Amount = 120m,
            OccurredAtUtc = DateTime.UtcNow,
            ExpenseKind = ExpenseKind.Discretionary,
        };

        var result = await gameLoop.ProcessTransactionAsync(tx, 0);

        Assert.Equal(0, fakeScoring.EvaluateCalls);
        Assert.Equal(0, fakeScoring.AddScoreEventCalls);
        Assert.Equal(0, result.PointsChanged);
        Assert.False(result.TriggeredReflection);
    }

    [Fact]
    public async Task ProcessTransactionAsync_WhenReflectionsDisabled_DoesNotCreateReflection()
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

        var factory = new TestDbContextFactory(options);
        var fakeBucketService = new FakeBucketService();
        fakeBucketService.Add(new Bucket { Id = 11, Name = "Variable", AllocatedAmount = 100m, IsSavings = false });
        fakeBucketService.SpentAmountsForPeriod[11] = 0m;

        var fakeScoring = new FakeScoringService();
        var fakeReflection = new FakeReflectionService();
        var savingsService = new SavingsService(factory);
        var settings = new FakeSettingsService(new AppSettings
        {
            EnableScoring = false,
            EnableReflections = false,
            EnableStreaks = false,
            LargeExpenseThreshold = 50,
            MonthStartDay = 1,
        });

        var gameLoop = new GameLoopService(factory, fakeBucketService, fakeScoring, fakeReflection, settings, savingsService, new FakeLevelService());

        var tx = new Transaction
        {
            Id = 43,
            BucketId = 11,
            Type = TransactionType.Expense,
            Amount = 70m,
            OccurredAtUtc = DateTime.UtcNow,
            ExpenseKind = ExpenseKind.Discretionary,
        };

        var result = await gameLoop.ProcessTransactionAsync(tx, 0);

        Assert.Equal(0, fakeReflection.CreateCalls);
        Assert.False(result.TriggeredReflection);
        Assert.Null(result.Reflection);
    }

    [Fact]
    public async Task ProcessTransactionAsync_TriggersLargeExpenseReflection_WhenThresholdReached()
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

        var factory = new TestDbContextFactory(options);
        var fakeBucketService = new FakeBucketService();
        fakeBucketService.Add(new Bucket { Id = 12, Name = "General", AllocatedAmount = 100m, IsSavings = false });
        fakeBucketService.SpentAmountsForPeriod[12] = 0m;

        var fakeScoring = new FakeScoringService();
        var fakeReflection = new FakeReflectionService();
        var savingsService = new SavingsService(factory);
        var settings = new FakeSettingsService(new AppSettings
        {
            EnableScoring = false,
            EnableReflections = true,
            EnableStreaks = false,
            LargeExpenseThreshold = 50,
            MonthStartDay = 1,
        });

        var gameLoop = new GameLoopService(factory, fakeBucketService, fakeScoring, fakeReflection, settings, savingsService, new FakeLevelService());

        var tx = new Transaction
        {
            Id = 44,
            BucketId = 12,
            Type = TransactionType.Expense,
            Amount = 50m,
            OccurredAtUtc = DateTime.UtcNow,
            ExpenseKind = ExpenseKind.Discretionary,
            IsImpulse = false,
        };

        var result = await gameLoop.ProcessTransactionAsync(tx, 0);

        Assert.Equal(1, fakeReflection.CreateCalls);
        Assert.Equal(ReflectionTrigger.LargeExpense, fakeReflection.LastTrigger);
        Assert.True(result.TriggeredReflection);
        Assert.NotNull(result.Reflection);
    }

    [Fact]
    public async Task ProcessTransactionAsync_WhenBudgetShortfall_DipsSavingsBeforeEmergency()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CoinStackDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupDb = new CoinStackDbContext(options))
        {
            await setupDb.Database.EnsureCreatedAsync();
            setupDb.Buckets.Add(new Bucket { Id = 15, Name = "General", AllocatedAmount = 500m, IsSavings = false });
            setupDb.SavingsState.Add(new SavingsState
            {
                FallbackEnabled = true,
                Total = 100m,
                Available = 50m,
                EmergencyTotal = 100m,
                EmergencyAvailable = 60m
            });

            setupDb.AppSettings.Add(new AppSettings
            {
                MonthlyIncome = 100m,
                EnableScoring = true,
                EnableReflections = false,
                EnableStreaks = false,
                MonthStartDay = 1,
                EnableEmergencyFallback = true,
            });

            setupDb.Transactions.Add(new Transaction
            {
                Description = "Existing expense",
                Amount = 90m,
                Type = TransactionType.Expense,
                OccurredAtUtc = DateTime.UtcNow,
                ExpenseKind = ExpenseKind.Discretionary,
                BucketId = 15,
            });

            await setupDb.SaveChangesAsync();
        }

        var factory = new TestDbContextFactory(options);
        var fakeBucketService = new FakeBucketService();
        fakeBucketService.Add(new Bucket { Id = 15, Name = "General", AllocatedAmount = 500m, IsSavings = false });
        fakeBucketService.SpentAmountsForPeriod[15] = 110m;

        var fakeScoring = new FakeScoringService();
        var fakeReflection = new FakeReflectionService();
        var settings = new FakeSettingsService(new AppSettings
        {
            MonthlyIncome = 100m,
            EnableScoring = true,
            EnableReflections = false,
            EnableStreaks = false,
            MonthStartDay = 1,
            EnableEmergencyFallback = true,
        });
        var savingsService = new SavingsService(factory);

        var gameLoop = new GameLoopService(factory, fakeBucketService, fakeScoring, fakeReflection, settings, savingsService, new FakeLevelService());

        Transaction tx;
        await using (var txDb = new CoinStackDbContext(options))
        {
            tx = new Transaction
            {
                BucketId = 15,
                Type = TransactionType.Expense,
                Amount = 20m,
                Description = "Overspend",
                OccurredAtUtc = DateTime.UtcNow,
                ExpenseKind = ExpenseKind.Discretionary,
            };
            txDb.Transactions.Add(tx);
            await txDb.SaveChangesAsync();
        }

        var result = await gameLoop.ProcessTransactionAsync(tx, 0);

        await using var verifyDb = new CoinStackDbContext(options);
        var state = await verifyDb.SavingsState.FirstAsync();
        Assert.Equal(40m, state.Available);
        Assert.Equal(60m, state.EmergencyAvailable);
        Assert.Equal(-15, result.PointsChanged);
    }

    [Fact]
    public async Task ProcessTransactionAsync_WhenSavingsInsufficient_UsesEmergencyWithDoublePenalty()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CoinStackDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupDb = new CoinStackDbContext(options))
        {
            await setupDb.Database.EnsureCreatedAsync();
            setupDb.Buckets.Add(new Bucket { Id = 16, Name = "General", AllocatedAmount = 500m, IsSavings = false });
            setupDb.SavingsState.Add(new SavingsState
            {
                FallbackEnabled = true,
                Total = 100m,
                Available = 5m,
                EmergencyTotal = 100m,
                EmergencyAvailable = 100m
            });

            setupDb.Transactions.Add(new Transaction
            {
                Description = "Existing expense",
                Amount = 95m,
                Type = TransactionType.Expense,
                OccurredAtUtc = DateTime.UtcNow,
                ExpenseKind = ExpenseKind.Discretionary,
                BucketId = 16,
            });

            await setupDb.SaveChangesAsync();
        }

        var factory = new TestDbContextFactory(options);
        var fakeBucketService = new FakeBucketService();
        fakeBucketService.Add(new Bucket { Id = 16, Name = "General", AllocatedAmount = 500m, IsSavings = false });
        fakeBucketService.SpentAmountsForPeriod[16] = 115m;

        var fakeScoring = new FakeScoringService();
        var fakeReflection = new FakeReflectionService();
        var settings = new FakeSettingsService(new AppSettings
        {
            MonthlyIncome = 100m,
            EnableScoring = true,
            EnableReflections = false,
            EnableStreaks = false,
            MonthStartDay = 1,
            EnableEmergencyFallback = true,
        });
        var savingsService = new SavingsService(factory);

        var gameLoop = new GameLoopService(factory, fakeBucketService, fakeScoring, fakeReflection, settings, savingsService, new FakeLevelService());

        Transaction tx;
        await using (var txDb = new CoinStackDbContext(options))
        {
            tx = new Transaction
            {
                BucketId = 16,
                Type = TransactionType.Expense,
                Amount = 20m,
                Description = "Overspend hard",
                OccurredAtUtc = DateTime.UtcNow,
                ExpenseKind = ExpenseKind.Discretionary,
            };
            txDb.Transactions.Add(tx);
            await txDb.SaveChangesAsync();
        }

        var result = await gameLoop.ProcessTransactionAsync(tx, 0);

        await using var verifyDb = new CoinStackDbContext(options);
        var state = await verifyDb.SavingsState.FirstAsync();
        Assert.Equal(0m, state.Available);
        Assert.Equal(90m, state.EmergencyAvailable);
        Assert.Equal(-45, result.PointsChanged);
    }
}
