using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

public sealed class TransactionServiceTests
{
    [Fact]
    public async Task CreateWithGameLoopAsync_Persists_Transaction_And_Returns_Game_Result()
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
        var fakeGameLoop = new FakeGameLoopService
        {
            NextResult = new GameTransactionResult
            {
                PointsChanged = 9,
                Message = "scored"
            }
        };

        var service = new TransactionService(factory, fakeGameLoop);

        var transaction = new Transaction
        {
            Description = "Groceries",
            Amount = 120m,
            Type = TransactionType.Expense,
            OccurredAtUtc = DateTime.UtcNow,
            ExpenseKind = ExpenseKind.Mandatory
        };

        var (created, result) = await service.CreateWithGameLoopAsync(transaction, 0);

        Assert.True(created.Id > 0);
        Assert.Equal(9, result.PointsChanged);
        Assert.Equal("scored", result.Message);
        Assert.Equal(1, fakeGameLoop.ProcessTransactionCalls);

        await using var verifyDb = new CoinStackDbContext(options);
        var count = await verifyDb.Transactions.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task UpdateAsync_Reverts_Original_Impact_And_Reprocesses_Transaction()
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
        var fakeGameLoop = new FakeGameLoopService();
        var service = new TransactionService(factory, fakeGameLoop);

        var original = await service.CreateAsync(new Transaction
        {
            Description = "Original",
            Amount = 50m,
            Type = TransactionType.Expense,
            OccurredAtUtc = DateTime.UtcNow,
            ExpenseKind = ExpenseKind.Mandatory
        });

        var updated = new Transaction
        {
            Id = original.Id,
            Description = "Updated",
            Amount = 80m,
            Type = TransactionType.Expense,
            OccurredAtUtc = original.OccurredAtUtc,
            ExpenseKind = ExpenseKind.Mandatory
        };

        await service.UpdateAsync(updated);

        Assert.Equal(1, fakeGameLoop.RevertCalls);
        Assert.Equal(1, fakeGameLoop.ProcessTransactionCalls);

        await using var verifyDb = new CoinStackDbContext(options);
        var saved = await verifyDb.Transactions.SingleAsync(t => t.Id == original.Id);
        Assert.Equal("Updated", saved.Description);
        Assert.Equal(80m, saved.Amount);
    }

    [Fact]
    public async Task DeleteAsync_Reverts_Original_Impact_Before_Delete()
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
        var fakeGameLoop = new FakeGameLoopService();
        var service = new TransactionService(factory, fakeGameLoop);

        var original = await service.CreateAsync(new Transaction
        {
            Description = "To delete",
            Amount = 40m,
            Type = TransactionType.Expense,
            OccurredAtUtc = DateTime.UtcNow,
            ExpenseKind = ExpenseKind.Mandatory
        });

        await service.DeleteAsync(original.Id);

        Assert.Equal(1, fakeGameLoop.RevertCalls);
        Assert.Equal(0, fakeGameLoop.ProcessTransactionCalls);

        await using var verifyDb = new CoinStackDbContext(options);
        var exists = await verifyDb.Transactions.AnyAsync(t => t.Id == original.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task UpdateAsync_Persists_Turning_AutoDeduct_Off()
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

        var service = new TransactionService(new TestDbContextFactory(options), new FakeGameLoopService());

        var template = await service.CreateAsync(new Transaction
        {
            Description = "Gym",
            Amount = 499m,
            Type = TransactionType.Expense,
            OccurredAtUtc = DateTime.UtcNow,
            AutoDeduct = true
        });

        template.AutoDeduct = false;
        await service.UpdateAsync(template);

        await using var verifyDb = new CoinStackDbContext(options);
        Assert.False((await verifyDb.Transactions.SingleAsync(t => t.Id == template.Id)).AutoDeduct);
    }

    [Fact]
    public async Task The_Bank_Balance_Is_The_Last_Balance_Plus_Everything_Since()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CoinStackDbContext>().UseSqlite(connection).Options;
        var asOf = new DateTime(2026, 10, 1, 12, 0, 0);

        await using (var db = new CoinStackDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Accounts.Add(new Account { Name = "Main account", IsDefault = true, Balance = 5000m, BalanceAsOfUtc = asOf });

            // Rows with no account are the main account's, as rows saved before accounts existed are.
            db.Transactions.AddRange(
                new Transaction { Description = "Before the balance", Amount = 999m, Type = TransactionType.Expense, OccurredAtUtc = asOf.AddDays(-1) },
                new Transaction { Description = "Salary", Amount = 20000m, Type = TransactionType.Income, OccurredAtUtc = asOf.AddDays(2) },
                new Transaction { Description = "Rent", Amount = 8000m, Type = TransactionType.Expense, OccurredAtUtc = asOf.AddDays(3) },
                new Transaction { Description = "To savings", Amount = 3000m, Type = TransactionType.Transfer, OccurredAtUtc = asOf.AddDays(4) });
            await db.SaveChangesAsync();
        }

        var service = new TransactionService(new TestDbContextFactory(options), new FakeGameLoopService());

        // The balance from the statement, plus the salary, minus the rent. Earlier rows don't count, and nor
        // does a transfer from before accounts existed, which has nowhere to go.
        Assert.Equal(17000m, await service.GetEffectiveBankBalanceAsync());

        await using (var db = new CoinStackDbContext(options))
        {
            (await db.Accounts.SingleAsync()).BalanceAsOfUtc = null;
            await db.SaveChangesAsync();
        }

        Assert.Equal(5000m, await service.GetEffectiveBankBalanceAsync());
    }
}
