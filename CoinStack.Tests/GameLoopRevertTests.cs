using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

public sealed class GameLoopRevertTests
{
    private const int BucketId = 30;

    [Fact]
    public async Task Revert_Restores_Shortfall_Savings_Draw_And_Xp()
    {
        await using var fixture = await Fixture.CreateAsync();
        var tx = await fixture.AddExpenseAsync(300m, ExpenseKind.Discretionary);

        // Income 100, spend 300: a R200 shortfall is covered by moving R200 from savings to the main account.
        await fixture.GameLoop.ProcessTransactionAsync(tx, 0);
        Assert.Equal(800m, await fixture.SavingsAvailableAsync());
        Assert.Equal(-100m, await fixture.MainBalanceAsync());

        await fixture.GameLoop.RevertTransactionImpactAsync(tx, CancellationToken.None);

        Assert.Equal(1000m, await fixture.SavingsAvailableAsync());
        Assert.Equal(-300m, await fixture.MainBalanceAsync());
        Assert.Equal(-5, fixture.Levels.LastXpAmount);
        await using var db = fixture.NewDb();
        Assert.Empty(await db.SavingsFallbackEvents.ToListAsync());
        Assert.DoesNotContain(await db.Transactions.ToListAsync(), t => t.Type == TransactionType.Transfer);
    }

    [Fact]
    public async Task Edit_Is_Net_Neutral_For_Savings()
    {
        await using var fixture = await Fixture.CreateAsync();
        var tx = await fixture.AddExpenseAsync(300m, ExpenseKind.Discretionary);

        await fixture.GameLoop.ProcessTransactionAsync(tx, 0);

        // An edit reverts then re-processes; three edits used to draw R800 instead of R200.
        for (var i = 0; i < 3; i++)
        {
            await fixture.GameLoop.RevertTransactionImpactAsync(tx, CancellationToken.None);
            await fixture.GameLoop.ProcessTransactionAsync(tx, 0);
        }

        Assert.Equal(800m, await fixture.SavingsAvailableAsync());
    }

    [Fact]
    public async Task Force_Majeure_Does_Not_Trigger_Shortfall_Cover()
    {
        await using var fixture = await Fixture.CreateAsync();
        var tx = await fixture.AddExpenseAsync(300m, ExpenseKind.ForceMajeure);

        await fixture.GameLoop.ProcessTransactionAsync(tx, 0);

        // The Transactions page withdraws for force majeure explicitly; the game loop must not do it too.
        Assert.Equal(1000m, await fixture.SavingsAvailableAsync());
    }

    [Fact]
    public async Task Deleting_A_Force_Majeure_Expense_Refunds_The_Explicit_Withdrawal()
    {
        await using var fixture = await Fixture.CreateAsync();
        var tx = await fixture.AddExpenseAsync(300m, ExpenseKind.ForceMajeure);

        await fixture.Savings.WithdrawForEmergencyAsync(300m, "Force majeure: Tow truck", tx.Id);
        Assert.Equal(700m, await fixture.SavingsAvailableAsync());

        var transactions = new TransactionService(fixture.Factory, fixture.GameLoop);

        // An edit keeps the user's withdrawal...
        await transactions.UpdateAsync(tx);
        Assert.Equal(700m, await fixture.SavingsAvailableAsync());

        // ...a delete gives it back.
        await transactions.DeleteAsync(tx.Id);
        Assert.Equal(1000m, await fixture.SavingsAvailableAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<CoinStackDbContext> _options;

        private Fixture(SqliteConnection connection, DbContextOptions<CoinStackDbContext> options)
        {
            _connection = connection;
            _options = options;
            Factory = new TestDbContextFactory(options);
            Savings = new SavingsService(Factory);

            var buckets = new FakeBucketService();
            buckets.Add(new Bucket { Id = BucketId, Name = "Everyday", AllocatedAmount = 5000m });

            var settings = new FakeSettingsService(new AppSettings
            {
                MonthlyIncome = 100m,
                MonthStartDay = 1,
                EnableScoring = false,
                EnableReflections = false,
                EnableEmergencyFallback = false
            });

            GameLoop = new GameLoopService(Factory, buckets, new FakeScoringService(), new FakeReflectionService(), settings, Savings, Levels);
        }

        public TestDbContextFactory Factory { get; }
        public SavingsService Savings { get; }
        public FakeLevelService Levels { get; } = new();
        public GameLoopService GameLoop { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<CoinStackDbContext>().UseSqlite(connection).Options;

            await using (var db = new CoinStackDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                db.Buckets.Add(new Bucket { Id = BucketId, Name = "Everyday", AllocatedAmount = 5000m });
                var anchor = DateTime.UtcNow.AddDays(-1);
                db.Accounts.AddRange(
                    new Account { Name = "Main account", Type = AccountType.Cheque, IsDefault = true, Balance = 0m, BalanceAsOfUtc = anchor },
                    new Account { Name = "Savings", Type = AccountType.Savings, Balance = 1000m, BalanceAsOfUtc = anchor });
                db.SavingsState.Add(new SavingsState { FallbackEnabled = true });
                await db.SaveChangesAsync();
            }

            return new Fixture(connection, options);
        }

        public CoinStackDbContext NewDb() => new(_options);

        public async Task<Transaction> AddExpenseAsync(decimal amount, ExpenseKind kind)
        {
            await using var db = NewDb();
            var tx = new Transaction
            {
                Description = "Spend",
                Amount = amount,
                Type = TransactionType.Expense,
                ExpenseKind = kind,
                BucketId = BucketId,
                OccurredAtUtc = DateTime.UtcNow
            };
            db.Transactions.Add(tx);
            await db.SaveChangesAsync();
            return tx;
        }

        public async Task<decimal> SavingsAvailableAsync() => (await Savings.GetSnapshotAsync()).Available;

        public async Task<decimal> MainBalanceAsync() => await new AccountService(Factory).GetSpendableBalanceAsync();

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }
}
