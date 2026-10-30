using System.Globalization;
using System.Text;
using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CoinStack.Services.Import;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

/// <summary>
/// A credit card is an account linked to its debt: purchases on the card are spending and raise the debt,
/// and paying the card is a transfer that lowers it.
/// </summary>
public sealed class CreditCardTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<CoinStackDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _mainId;
    private int _cardId;
    private int _debtId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<CoinStackDbContext>().UseSqlite(_connection).Options;
        _factory = new TestDbContextFactory(_options);

        await using var db = new CoinStackDbContext(_options);
        await db.Database.EnsureCreatedAsync();
        await DatabaseInitializer.SeedMissingDefaultCategoriesAsync(db, default);
        var main = new Account { Name = "Main account", Type = AccountType.Cheque, IsDefault = true, AccountNumber = "62000000001", Balance = 5000m, BalanceAsOfUtc = DateTime.UtcNow.AddDays(-60) };
        var card = new Account { Name = "Card", Type = AccountType.CreditCard, Balance = 0m, BalanceAsOfUtc = DateTime.UtcNow.AddDays(-60) };
        var debt = new DebtAccount
        {
            Name = "FNB Credit Card", TotalAmount = 20000m, CurrentBalance = 1000m, MonthlyPaymentAmount = 500m,
            InterestRatePercent = 24m, PaymentStartDateUtc = DateTime.UtcNow.Date.AddMonths(-6), InterestAccruedThroughUtc = DateTime.UtcNow.Date
        };
        db.Accounts.AddRange(main, card);
        db.DebtAccounts.Add(debt);
        await db.SaveChangesAsync();
        _mainId = main.Id;
        _cardId = card.Id;
        _debtId = debt.Id;
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Linking_Starts_The_Card_At_What_The_Debt_Owes_And_The_Debt_Then_Follows_The_Card()
    {
        await Accounts().LinkDebtAsync(_cardId, _debtId);

        Assert.Equal(-1000m, await CardBalanceAsync());
        Assert.Equal(1000m, await DebtBalanceAsync());

        await AddAsync(new Transaction { Description = "Petrol", Amount = 300m, Type = TransactionType.Expense, AccountId = _cardId, OccurredAtUtc = DateTime.UtcNow.AddMinutes(1) });
        Assert.Equal(1300m, await DebtBalanceAsync());

        await Accounts().TransferAsync(_mainId, _cardId, 800m, DateTime.UtcNow.AddMinutes(2));
        Assert.Equal(500m, await DebtBalanceAsync());
        Assert.Equal(4200m, await Accounts().GetSpendableBalanceAsync());
    }

    [Fact]
    public async Task Recording_A_Payment_On_A_Linked_Debt_Is_A_Transfer_To_The_Card()
    {
        await Accounts().LinkDebtAsync(_cardId, _debtId);

        await new DebtService(_factory).RecordPaymentAsync(_debtId, 400m);

        await using var db = new CoinStackDbContext(_options);
        var payment = await db.Transactions.SingleAsync();
        Assert.Equal(TransactionType.Transfer, payment.Type);
        Assert.Equal(_mainId, payment.AccountId);
        Assert.Equal(_cardId, payment.TransferAccountId);
        Assert.Equal(600m, await DebtBalanceAsync());
    }

    [Fact]
    public async Task An_Expense_Tagged_With_A_Linked_Debt_Does_Not_Change_It_Twice()
    {
        await Accounts().LinkDebtAsync(_cardId, _debtId);

        var transactions = new TransactionService(_factory, new FakeGameLoopService());
        await transactions.CreateAsync(new Transaction
        {
            Description = "Old-style card payment", Amount = 200m, Type = TransactionType.Expense, AccountId = _mainId, DebtAccountId = _debtId, OccurredAtUtc = DateTime.UtcNow
        });

        // The card account didn't move, so neither did the debt.
        Assert.Equal(1000m, await DebtBalanceAsync());
        await using var db = new CoinStackDbContext(_options);
        Assert.Equal(1000m, (await db.DebtAccounts.SingleAsync()).CurrentBalance);
    }

    [Fact]
    public async Task Interest_On_A_Linked_Card_Is_Charged_To_The_Card()
    {
        await Accounts().LinkDebtAsync(_cardId, _debtId);
        await using (var db = new CoinStackDbContext(_options))
        {
            (await db.DebtAccounts.SingleAsync()).InterestAccruedThroughUtc = DateTime.UtcNow.Date.AddDays(-40);
            await db.SaveChangesAsync();
        }

        var posted = await new DebtService(_factory).AccrueInterestAsync();

        Assert.True(posted > 0m);
        await using var check = new CoinStackDbContext(_options);
        var charges = await check.Transactions.Where(t => t.AccountId == _cardId && t.Type == TransactionType.Expense).ToListAsync();
        Assert.Equal(posted, charges.Sum(c => c.Amount));
        Assert.All(charges, c => Assert.Equal(TransactionConventions.AutoSource, c.Source));
        Assert.Equal(1000m + posted, await DebtBalanceAsync());
    }

    [Fact]
    public async Task A_Monthly_Debit_Order_For_A_Linked_Debt_Is_Copied_As_A_Transfer_To_The_Card()
    {
        await Accounts().LinkDebtAsync(_cardId, _debtId);
        var periodStart = DateTime.UtcNow;
        await AddAsync(new Transaction
        {
            Description = "Card debit order", Amount = 500m, Type = TransactionType.Expense, AccountId = _mainId,
            DebtAccountId = _debtId, AutoDeduct = true, ExpenseKind = ExpenseKind.Mandatory, OccurredAtUtc = periodStart.AddMonths(-2)
        });

        await new TransactionService(_factory, new FakeGameLoopService()).ApplyAutoDeductionsForBudgetPeriodAsync(1, periodStart);

        await using var db = new CoinStackDbContext(_options);
        var copy = await db.Transactions.SingleAsync(t => t.AutoDeductTemplateId != null);
        Assert.Equal(TransactionType.Transfer, copy.Type);
        Assert.Equal(_mainId, copy.AccountId);
        Assert.Equal(_cardId, copy.TransferAccountId);
        Assert.Null(copy.DebtAccountId);
    }

    [Fact]
    public async Task Paying_The_Card_On_A_Cheque_Statement_Is_A_Transfer_To_The_Card()
    {
        var import = new BankStatementImportService(_factory, new CompositeBankFeed([new FnbPdfBankFeed(), new FnbHistoryBankFeed()]));
        var csv = string.Create(CultureInfo.InvariantCulture,
            $"Account,Date,Description,Amount,Balance\n62000000001,2026/07/03,FNB App Transfer To Credit Card,-750.00,4250.00\n");

        var preview = await import.BuildPreviewAsync(new MemoryStream(Encoding.UTF8.GetBytes(csv)), "cheque.csv");

        var payment = preview!.Candidates.Single();
        Assert.Equal(TransactionType.Transfer, payment.Type);
        Assert.Equal(_cardId, payment.CounterAccountId);
        Assert.True(payment.Include);
    }

    [Fact]
    public async Task Unlinking_Leaves_The_Debt_Where_The_Card_Was()
    {
        var accounts = Accounts();
        await accounts.LinkDebtAsync(_cardId, _debtId);
        await AddAsync(new Transaction { Description = "Groceries", Amount = 250m, Type = TransactionType.Expense, AccountId = _cardId, OccurredAtUtc = DateTime.UtcNow.AddMinutes(1) });

        await accounts.LinkDebtAsync(_cardId, null);

        Assert.Equal(1250m, await DebtBalanceAsync());
        await using var db = new CoinStackDbContext(_options);
        Assert.Null((await db.Accounts.SingleAsync(a => a.Id == _cardId)).DebtAccountId);
    }

    [Fact]
    public async Task Only_A_Card_Can_Be_A_Debt_And_A_Debt_Has_One_Card()
    {
        var accounts = Accounts();
        await Assert.ThrowsAsync<InvalidOperationException>(() => accounts.LinkDebtAsync(_mainId, _debtId));

        await accounts.LinkDebtAsync(_cardId, _debtId);
        var second = await accounts.CreateAsync(new Account { Name = "Second card", Type = AccountType.CreditCard });
        await Assert.ThrowsAsync<InvalidOperationException>(() => accounts.LinkDebtAsync(second.Id, _debtId));
    }

    private AccountService Accounts() => new(_factory);

    private async Task<decimal> CardBalanceAsync()
        => (await Accounts().GetBalancesAsync()).Single(b => b.Account.Id == _cardId).Balance;

    private async Task<decimal> DebtBalanceAsync()
        => (await new DebtService(_factory).GetByIdAsync(_debtId))!.CurrentBalance;

    private async Task AddAsync(Transaction transaction)
    {
        await using var db = new CoinStackDbContext(_options);
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
    }
}
