using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

/// <summary>Savings and the emergency fund are accounts: putting money in or taking it out is a transfer.</summary>
public sealed class SavingsAccountsTests : IAsyncLifetime
{
    private const int GoalBucketId = 7;

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<CoinStackDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<CoinStackDbContext>().UseSqlite(_connection).Options;
        _factory = new TestDbContextFactory(_options);

        await using var db = new CoinStackDbContext(_options);
        await db.Database.EnsureCreatedAsync();
        var anchor = DateTime.UtcNow.AddDays(-1);
        db.Accounts.AddRange(
            new Account { Name = "Main account", Type = AccountType.Cheque, IsDefault = true, Balance = 5000m, BalanceAsOfUtc = anchor },
            new Account { Name = "Savings", Type = AccountType.Savings, Balance = 1000m, BalanceAsOfUtc = anchor },
            new Account { Name = "Emergency fund", Type = AccountType.EmergencyFund, Balance = 300m, BalanceAsOfUtc = anchor });
        db.SavingsState.Add(new SavingsState { FallbackEnabled = true });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Moving_Money_Into_Reserves_Takes_It_From_The_Main_Account()
    {
        var savings = new SavingsService(_factory);

        await savings.AddToSavingsAsync(400m, "Month-end top-up");
        await savings.AddToEmergencyFundAsync(100m, "");

        var snapshot = await savings.GetSnapshotAsync();
        Assert.Equal(1400m, snapshot.SavingsBalance);
        Assert.Equal(400m, snapshot.EmergencyBalance);
        Assert.Equal(4500m, await MainBalanceAsync());

        await using var db = new CoinStackDbContext(_options);
        Assert.Equal(2, await db.Transactions.CountAsync(t => t.Type == TransactionType.Transfer));
    }

    [Fact]
    public async Task The_Monthly_Run_Moves_The_Planned_Amount_And_Adds_Interest_Without_Calling_It_Income()
    {
        await using (var db = new CoinStackDbContext(_options))
        {
            db.AppSettings.Add(new AppSettings { MonthlySavingsAmount = 500m, SavingsInterestRate = 0.12m, SavingsInterestIsYearly = true });
            await db.SaveChangesAsync();
        }

        var summary = await new SavingsService(_factory).CalculateMonthAsync();

        Assert.NotNull(summary);
        Assert.Equal(500m, summary.Base);
        Assert.Equal(15m, summary.Interest); // 1% of (1 000 + 500)
        Assert.Equal(1515m, summary.RunningTotal);
        Assert.Equal(1515m, (await new SavingsService(_factory).GetSnapshotAsync()).SavingsBalance);
        Assert.Equal(4500m, await MainBalanceAsync());

        await using var check = new CoinStackDbContext(_options);
        var transfer = await check.Transactions.SingleAsync();
        Assert.Equal(TransactionType.Transfer, transfer.Type);
        Assert.Equal(TransactionConventions.AutoSource, transfer.Source);
        Assert.False(await check.Transactions.AnyAsync(t => t.Type == TransactionType.Income));
    }

    [Fact]
    public async Task Shortfall_Cover_Uses_Free_Savings_Then_The_Emergency_Fund_And_Leaves_Goal_Money()
    {
        await using (var db = new CoinStackDbContext(_options))
        {
            (await db.SavingsState.SingleAsync()).Reserved = 800m;
            await db.SaveChangesAsync();
        }

        var savings = new SavingsService(_factory);
        var covered = await savings.ApplyReserveFallbackAsync(350m, "Overspent", "Groceries", allowEmergencyFund: true, transactionId: 99);

        // Only R200 of the R1 000 in savings is free; the other R150 comes from the emergency fund.
        Assert.Equal(200m, covered.SavingsUsed);
        Assert.Equal(150m, covered.EmergencyUsed);
        Assert.Equal(5350m, await MainBalanceAsync());

        await savings.RestoreForTransactionAsync(99);

        var snapshot = await savings.GetSnapshotAsync();
        Assert.Equal(1000m, snapshot.SavingsBalance);
        Assert.Equal(300m, snapshot.EmergencyBalance);
        Assert.Equal(5000m, await MainBalanceAsync());
    }

    [Fact]
    public async Task Undoing_A_Draw_From_Before_Accounts_Puts_The_Money_Back_In_Savings()
    {
        await using (var db = new CoinStackDbContext(_options))
        {
            db.SavingsFallbackEvents.Add(new SavingsFallbackEvent { AmountUsed = 120m, Reason = "Old draw", SourceName = "Everyday", TransactionId = 42 });
            db.SavingsFallbackEvents.Add(new SavingsFallbackEvent { AmountUsed = 30m, Reason = "Old draw", SourceName = "Emergency Fund", FromEmergencyFund = true, TransactionId = 42 });
            await db.SaveChangesAsync();
        }

        var savings = new SavingsService(_factory);
        Assert.Equal(150m, await savings.RestoreForTransactionAsync(42));

        var snapshot = await savings.GetSnapshotAsync();
        Assert.Equal(1120m, snapshot.SavingsBalance);
        Assert.Equal(330m, snapshot.EmergencyBalance);

        // The draw never reached the main account, so nothing is taken back from it.
        Assert.Equal(5000m, await MainBalanceAsync());
    }

    [Fact]
    public async Task A_Savings_Bucket_Row_With_No_Account_Lands_In_Savings()
    {
        await using (var db = new CoinStackDbContext(_options))
        {
            db.Buckets.Add(new Bucket { Id = GoalBucketId, Name = "Holiday", IsSavings = true });
            await db.SaveChangesAsync();
        }

        var transactions = new TransactionService(_factory, new FakeGameLoopService());
        var created = await transactions.CreateAsync(new Transaction
        {
            Description = "Bonus to savings", Amount = 250m, Type = TransactionType.Income, BucketId = GoalBucketId, OccurredAtUtc = DateTime.UtcNow
        });

        await using var check = new CoinStackDbContext(_options);
        Assert.Equal(AccountType.Savings, (await check.Accounts.SingleAsync(a => a.Id == created.AccountId)).Type);
        Assert.Equal(1250m, (await new SavingsService(_factory).GetSnapshotAsync()).SavingsBalance);
    }

    [Fact]
    public async Task Goal_Income_In_Savings_Is_Set_Aside_For_The_Goal_Until_It_Is_Undone()
    {
        int goalId;
        int savingsId;
        await using (var db = new CoinStackDbContext(_options))
        {
            var goal = new Goal { Name = "Holiday", TargetAmount = 5000m };
            db.Goals.Add(goal);
            await db.SaveChangesAsync();
            goalId = goal.Id;
            db.Buckets.Add(new Bucket { Id = GoalBucketId, Name = "Holiday", IsSavings = true, GoalId = goalId });
            await db.SaveChangesAsync();
            savingsId = (await db.Accounts.SingleAsync(a => a.Type == AccountType.Savings)).Id;
        }

        var buckets = new FakeBucketService();
        buckets.Add(new Bucket { Id = GoalBucketId, Name = "Holiday", IsSavings = true, GoalId = goalId });
        var settings = new FakeSettingsService(new AppSettings { EnableScoring = false, EnableReflections = false });
        var savings = new SavingsService(_factory);
        var gameLoop = new GameLoopService(_factory, buckets, new FakeScoringService(), new FakeReflectionService(), settings, savings, new FakeLevelService());

        var income = new Transaction
        {
            Description = "Gift for the holiday", Amount = 600m, Type = TransactionType.Income,
            BucketId = GoalBucketId, AccountId = savingsId, OccurredAtUtc = DateTime.UtcNow
        };
        await using (var db = new CoinStackDbContext(_options))
        {
            db.Transactions.Add(income);
            await db.SaveChangesAsync();
        }

        await gameLoop.ProcessTransactionAsync(income, 0);

        var afterIncome = await savings.GetSnapshotAsync();
        Assert.Equal(1600m, afterIncome.SavingsBalance);
        Assert.Equal(600m, afterIncome.Reserved);
        Assert.Equal(1000m, afterIncome.Available);

        await gameLoop.RevertTransactionImpactAsync(income, CancellationToken.None);

        Assert.Equal(0m, (await savings.GetSnapshotAsync()).Reserved);
        await using var check = new CoinStackDbContext(_options);
        Assert.Equal(0m, (await check.Goals.SingleAsync()).CurrentAmount);
    }

    private async Task<decimal> MainBalanceAsync() => await new AccountService(_factory).GetSpendableBalanceAsync();
}
