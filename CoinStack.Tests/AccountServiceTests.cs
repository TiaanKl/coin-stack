using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

public sealed class AccountServiceTests : IAsyncLifetime
{
    private static readonly DateTime AsOf = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
    private DbContextOptions<CoinStackDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<CoinStackDbContext>().UseSqlite(_connection).Options;
        await using var db = new CoinStackDbContext(_options);
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task A_Balance_Is_Its_Anchor_Plus_Later_Income_Spending_And_Transfers()
    {
        var (main, savings, card) = await SeedAccountsAsync();
        await AddAsync(
            new Transaction { Description = "Before the anchor", Amount = 999m, Type = TransactionType.Expense, AccountId = main, OccurredAtUtc = AsOf.AddDays(-1) },
            new Transaction { Description = "Salary, saved before accounts", Amount = 300m, Type = TransactionType.Income, OccurredAtUtc = AsOf.AddDays(1) },
            new Transaction { Description = "Groceries", Amount = 100m, Type = TransactionType.Expense, AccountId = main, OccurredAtUtc = AsOf.AddDays(2) },
            new Transaction { Description = "To savings", Amount = 400m, Type = TransactionType.Transfer, AccountId = main, TransferAccountId = savings, OccurredAtUtc = AsOf.AddDays(3) },
            new Transaction { Description = "Petrol on the card", Amount = 50m, Type = TransactionType.Expense, AccountId = card, OccurredAtUtc = AsOf.AddDays(4) },
            new Transaction { Description = "Card payment", Amount = 150m, Type = TransactionType.Transfer, AccountId = main, TransferAccountId = card, OccurredAtUtc = AsOf.AddDays(5) },
            new Transaction { Description = "Old transfer with nowhere to go", Amount = 999m, Type = TransactionType.Transfer, AccountId = main, OccurredAtUtc = AsOf.AddDays(6) });

        var service = CreateService();
        var balances = (await service.GetBalancesAsync()).ToDictionary(b => b.Account.Id, b => b.Balance);

        Assert.Equal(1000m + 300m - 100m - 400m - 150m, balances[main]);
        Assert.Equal(500m + 400m, balances[savings]);
        Assert.Equal(-200m - 50m + 150m, balances[card]);

        // Only the cheque account is money to spend.
        Assert.Equal(650m, await service.GetSpendableBalanceAsync());
    }

    [Fact]
    public async Task The_Daily_Change_In_Spendable_Money_Counts_Transfers_And_Skips_Card_Purchases()
    {
        var (main, savings, card) = await SeedAccountsAsync();
        var day = AsOf.AddDays(3).Date.AddHours(9);
        await AddAsync(
            new Transaction { Description = "Salary", Amount = 300m, Type = TransactionType.Income, AccountId = main, OccurredAtUtc = day },
            new Transaction { Description = "To savings", Amount = 400m, Type = TransactionType.Transfer, AccountId = main, TransferAccountId = savings, OccurredAtUtc = day },
            new Transaction { Description = "Petrol on the card", Amount = 50m, Type = TransactionType.Expense, AccountId = card, OccurredAtUtc = day });

        var change = await CreateService().GetDailySpendableChangeAsync(AsOf, AsOf.AddDays(10));

        Assert.Equal(-100m, change[day.Date]);
    }

    [Fact]
    public async Task Archived_Accounts_Are_Left_Out()
    {
        await SeedAccountsAsync();
        await using (var db = new CoinStackDbContext(_options))
        {
            db.Accounts.Add(new Account { Name = "Old cheque", Type = AccountType.Cheque, Balance = 5000m, BalanceAsOfUtc = AsOf, IsArchived = true });
            await db.SaveChangesAsync();
        }

        var service = CreateService();

        Assert.Equal(1000m, await service.GetSpendableBalanceAsync());
        Assert.DoesNotContain(await service.GetBalancesAsync(), b => b.Account.IsArchived);
        Assert.Contains(await service.GetBalancesAsync(includeArchived: true), b => b.Account.IsArchived);
    }

    [Fact]
    public async Task The_Main_Account_Is_Made_On_First_Use_With_No_Balance_Yet()
    {
        var service = CreateService();

        var first = await service.GetDefaultAsync();
        var second = await service.GetDefaultAsync();

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(AccountService.DefaultAccountName, first.Name);
        Assert.Equal(AccountType.Cheque, first.Type);
        Assert.Null(first.BalanceAsOfUtc);
    }

    [Fact]
    public async Task A_New_Account_Always_Gets_A_Balance_Date_So_Transfers_Into_It_Count()
    {
        var service = CreateService();
        var main = await service.GetDefaultAsync();
        var savings = await service.CreateAsync(new Account { Name = "Savings", Type = AccountType.Savings });

        Assert.NotNull(savings.BalanceAsOfUtc);
        Assert.False(savings.IsDefault);

        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        await service.TransferAsync(main.Id, savings.Id, 250m, _clock.UtcNow.UtcDateTime);

        Assert.Equal(250m, (await service.GetBalancesAsync()).Single(b => b.Account.Id == savings.Id).Balance);
    }

    [Fact]
    public async Task A_Transfer_Needs_Two_Different_Open_Accounts_And_An_Amount()
    {
        var (main, savings, _) = await SeedAccountsAsync();
        await using (var db = new CoinStackDbContext(_options))
        {
            (await db.Accounts.SingleAsync(a => a.Id == savings)).IsArchived = true;
            await db.SaveChangesAsync();
        }

        var service = CreateService();
        var when = _clock.UtcNow.UtcDateTime;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TransferAsync(main, main, 10m, when));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TransferAsync(main, savings, 0m, when));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TransferAsync(main, savings, 10m, when));
    }

    [Fact]
    public async Task A_New_Transfer_Needs_A_Destination_But_An_Older_One_Can_Still_Be_Edited()
    {
        var (main, _, _) = await SeedAccountsAsync();
        var older = new Transaction { Description = "Old transfer", Amount = 75m, Type = TransactionType.Transfer, AccountId = main, OccurredAtUtc = AsOf };
        await AddAsync(older);

        var transactions = new TransactionService(new TestDbContextFactory(_options), new FakeGameLoopService(), _clock);

        await Assert.ThrowsAsync<InvalidOperationException>(() => transactions.CreateAsync(new Transaction
        {
            Description = "Somewhere", Amount = 10m, Type = TransactionType.Transfer, AccountId = main, OccurredAtUtc = AsOf
        }));

        older.Description = "Old transfer, renamed";
        await transactions.UpdateAsync(older);

        await using var db = new CoinStackDbContext(_options);
        Assert.Equal("Old transfer, renamed", (await db.Transactions.SingleAsync(t => t.Id == older.Id)).Description);
    }

    [Fact]
    public async Task A_Transfer_Carries_No_Bucket_Or_Debt_And_Editing_Keeps_The_Accounts()
    {
        var (main, savings, card) = await SeedAccountsAsync();
        var transactions = new TransactionService(new TestDbContextFactory(_options), new FakeGameLoopService(), _clock);

        var created = await transactions.CreateAsync(new Transaction
        {
            Description = "To savings", Amount = 100m, Type = TransactionType.Transfer,
            AccountId = main, TransferAccountId = savings, BucketId = 42, IsImpulse = true, OccurredAtUtc = AsOf.AddDays(1)
        });
        Assert.Null(created.BucketId);
        Assert.False(created.IsImpulse);

        created.TransferAccountId = card;
        await transactions.UpdateAsync(created);

        await using var db = new CoinStackDbContext(_options);
        var saved = await db.Transactions.SingleAsync(t => t.Id == created.Id);
        Assert.Equal(main, saved.AccountId);
        Assert.Equal(card, saved.TransferAccountId);
    }

    [Fact]
    public async Task Card_Purchases_Are_Left_Out_Of_Cash_Spending()
    {
        var (main, _, card) = await SeedAccountsAsync();
        var midMonth = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        await AddAsync(
            new Transaction { Description = "Groceries", Amount = 100m, Type = TransactionType.Expense, AccountId = main, OccurredAtUtc = midMonth },
            new Transaction { Description = "Saved before accounts", Amount = 40m, Type = TransactionType.Expense, OccurredAtUtc = midMonth },
            new Transaction { Description = "Petrol on the card", Amount = 50m, Type = TransactionType.Expense, AccountId = card, OccurredAtUtc = midMonth });

        var transactions = new TransactionService(new TestDbContextFactory(_options), new FakeGameLoopService(), _clock);

        Assert.Equal(140m, await transactions.GetExpenseTotalForBudgetPeriodAsync(1, midMonth));
    }

    [Fact]
    public async Task Changing_The_Main_Account_Keeps_Older_Rows_On_The_Old_One()
    {
        var (main, _, _) = await SeedAccountsAsync();
        await AddAsync(new Transaction { Description = "Saved before accounts", Amount = 300m, Type = TransactionType.Income, OccurredAtUtc = AsOf.AddDays(1) });

        var service = CreateService();
        var second = await service.CreateAsync(new Account { Name = "Second cheque", Type = AccountType.Cheque, Balance = 0m, BalanceAsOfUtc = AsOf });
        await service.SetDefaultAsync(second.Id);

        var balances = (await service.GetBalancesAsync()).ToDictionary(b => b.Account.Id, b => b);
        Assert.Equal(1300m, balances[main].Balance);
        Assert.Equal(0m, balances[second.Id].Balance);
        Assert.True(balances[second.Id].Account.IsDefault);
        Assert.False(balances[main].Account.IsDefault);
    }

    [Fact]
    public async Task The_Main_Account_Stays_A_Cheque_Account_And_Is_Never_Archived()
    {
        var (main, _, _) = await SeedAccountsAsync();
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetArchivedAsync(main, true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(new Account { Id = main, Name = "Main", Type = AccountType.Savings }));
    }

    private AccountService CreateService() => new(new TestDbContextFactory(_options), _clock);

    private async Task<(int Main, int Savings, int Card)> SeedAccountsAsync()
    {
        await using var db = new CoinStackDbContext(_options);
        var main = new Account { Name = "Main account", Type = AccountType.Cheque, IsDefault = true, Balance = 1000m, BalanceAsOfUtc = AsOf };
        var savings = new Account { Name = "Savings", Type = AccountType.Savings, Balance = 500m, BalanceAsOfUtc = AsOf };
        var card = new Account { Name = "Card", Type = AccountType.CreditCard, Balance = -200m, BalanceAsOfUtc = AsOf };
        db.Accounts.AddRange(main, savings, card);
        await db.SaveChangesAsync();
        return (main.Id, savings.Id, card.Id);
    }

    private async Task AddAsync(params Transaction[] transactions)
    {
        await using var db = new CoinStackDbContext(_options);
        db.Transactions.AddRange(transactions);
        await db.SaveChangesAsync();
    }
}
