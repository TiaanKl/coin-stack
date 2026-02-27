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
        var settings = new FakeSettingsService(new AppSettings
        {
            EnableScoring = false,
            EnableReflections = false,
            EnableStreaks = false,
            LargeExpenseThreshold = 50,
            MonthStartDay = 1,
        });

        var gameLoop = new GameLoopService(factory, fakeBucketService, fakeScoring, fakeReflection, settings);

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
        var settings = new FakeSettingsService(new AppSettings
        {
            EnableScoring = false,
            EnableReflections = false,
            EnableStreaks = false,
            LargeExpenseThreshold = 50,
            MonthStartDay = 1,
        });

        var gameLoop = new GameLoopService(factory, fakeBucketService, fakeScoring, fakeReflection, settings);

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
        var settings = new FakeSettingsService(new AppSettings
        {
            EnableScoring = false,
            EnableReflections = true,
            EnableStreaks = false,
            LargeExpenseThreshold = 50,
            MonthStartDay = 1,
        });

        var gameLoop = new GameLoopService(factory, fakeBucketService, fakeScoring, fakeReflection, settings);

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
}
