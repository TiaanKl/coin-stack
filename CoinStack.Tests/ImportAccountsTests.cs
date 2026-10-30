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

/// <summary>A statement goes to the account with its number, and transfers between the person's accounts are recorded once.</summary>
public sealed class ImportAccountsTests : IAsyncLifetime
{
    private const string ChequeNumber = "62000000001";
    private const string SavingsNumber = "62000000002";
    private static readonly DateTime Anchor = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<CoinStackDbContext> _options = null!;
    private int _mainId;
    private int _savingsId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<CoinStackDbContext>().UseSqlite(_connection).Options;

        await using var db = new CoinStackDbContext(_options);
        await db.Database.EnsureCreatedAsync();
        await DatabaseInitializer.SeedMissingDefaultCategoriesAsync(db, default);
        var main = new Account { Name = "Main account", Type = AccountType.Cheque, IsDefault = true, AccountNumber = ChequeNumber, Balance = 1000m, BalanceAsOfUtc = Anchor };
        var savings = new Account { Name = "Savings", Type = AccountType.Savings, AccountNumber = SavingsNumber, Balance = 2000m, BalanceAsOfUtc = Anchor };
        db.Accounts.AddRange(main, savings);
        await db.SaveChangesAsync();
        _mainId = main.Id;
        _savingsId = savings.Id;
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task A_Statement_Goes_To_The_Account_With_Its_Number_And_Sets_Only_That_Balance()
    {
        var import = ImportService();
        var preview = await import.BuildPreviewAsync(Csv("62 0000 0000 2", ("2026/07/03", "Interest", 12m)), "savings.csv");

        Assert.NotNull(preview);
        Assert.Equal(_savingsId, preview.AccountId);

        await import.ImportAsync(preview);

        await using var db = new CoinStackDbContext(_options);
        Assert.All(await db.Transactions.ToListAsync(), t => Assert.Equal(_savingsId, t.AccountId));
        var balances = (await AccountService.GetBalancesAsync(db, default)).ToDictionary(b => b.Account.Id, b => b.Balance);
        Assert.Equal(5012m, balances[_savingsId]);
        Assert.Equal(1000m, balances[_mainId]);
    }

    [Fact]
    public async Task An_Unknown_Account_Has_To_Be_Chosen_And_Then_Learns_Its_Number()
    {
        var import = ImportService();
        var preview = await import.BuildPreviewAsync(Csv("62999999999", ("2026/07/03", "Groceries Spar", -80m)), "joint.csv");

        Assert.NotNull(preview);
        Assert.Null(preview.AccountId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => import.ImportAsync(preview));

        int jointId;
        await using (var db = new CoinStackDbContext(_options))
        {
            var joint = new Account { Name = "Joint", Type = AccountType.Cheque, Balance = 0m, BalanceAsOfUtc = Anchor };
            db.Accounts.Add(joint);
            await db.SaveChangesAsync();
            jointId = joint.Id;
        }

        preview.AccountId = jointId;
        await import.RefreshAsync(preview);
        await import.ImportAsync(preview);

        await using var check = new CoinStackDbContext(_options);
        Assert.Equal("62999999999", (await check.Accounts.SingleAsync(a => a.Id == jointId)).AccountNumber);
        Assert.Equal(jointId, (await check.Transactions.SingleAsync()).AccountId);
    }

    [Fact]
    public async Task The_First_Statement_Belongs_To_The_Only_Cheque_Account()
    {
        await using (var db = new CoinStackDbContext(_options))
        {
            (await db.Accounts.SingleAsync(a => a.Id == _mainId)).AccountNumber = null;
            await db.SaveChangesAsync();
        }

        var preview = await ImportService().BuildPreviewAsync(Csv("62123456789", ("2026/07/03", "Groceries Spar", -80m)), "first.csv");

        Assert.Equal(_mainId, preview!.AccountId);
    }

    [Fact]
    public async Task Files_From_Two_Accounts_Are_Not_Combined()
    {
        var import = ImportService();
        var cheque = await import.BuildPreviewAsync(Csv(ChequeNumber, ("2026/07/03", "Groceries Spar", -80m)), "cheque.csv");
        var savings = await import.BuildPreviewAsync(Csv(SavingsNumber, ("2026/07/03", "Interest", 12m)), "savings.csv");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => import.MergePreviewsAsync([cheque!, savings!]));
        Assert.Contains("different accounts", error.Message);
    }

    [Fact]
    public async Task The_Same_Row_On_Another_Account_Is_Not_A_Duplicate()
    {
        var import = ImportService();
        await import.ImportAsync((await import.BuildPreviewAsync(Csv(ChequeNumber, ("2026/07/03", "Bank Charges", -5m)), "cheque.csv"))!);

        var again = await import.BuildPreviewAsync(Csv(ChequeNumber, ("2026/07/03", "Bank Charges", -5m)), "cheque-again.csv");
        Assert.True(again!.Candidates.Single().AlreadyExists);

        var savings = await import.BuildPreviewAsync(Csv(SavingsNumber, ("2026/07/03", "Bank Charges", -5m)), "savings.csv");
        Assert.False(savings!.Candidates.Single().AlreadyExists);
    }

    [Fact]
    public async Task A_Payment_To_Savings_Is_Recorded_As_A_Transfer_Once_From_Either_Statement()
    {
        var import = ImportService();
        var cheque = await import.BuildPreviewAsync(Csv(ChequeNumber, ("2026/07/03", "Payment To Savings Holiday", -500m)), "cheque.csv");

        var transfer = cheque!.Candidates.Single();
        Assert.Equal(TransactionType.Transfer, transfer.Type);
        Assert.Equal(_savingsId, transfer.CounterAccountId);
        Assert.True(transfer.Include);

        var result = await import.ImportAsync(cheque);
        Assert.Equal(1, result.TransfersRecorded);

        await using (var db = new CoinStackDbContext(_options))
        {
            var saved = await db.Transactions.SingleAsync();
            Assert.Equal(_mainId, saved.AccountId);
            Assert.Equal(_savingsId, saved.TransferAccountId);
        }

        // The savings statement shows the same money arriving; once its other account is known, it's seen.
        var savings = await import.BuildPreviewAsync(Csv(SavingsNumber, ("2026/07/04", "Transfer From Cheque", 500m)), "savings.csv");
        var arriving = savings!.Candidates.Single();
        Assert.False(arriving.Include);

        arriving.CounterAccountId = _mainId;
        await import.RefreshAsync(savings);

        Assert.True(arriving.AlreadyExists);
        Assert.Equal("Already recorded from the other account", arriving.SkipReason);
    }

    [Fact]
    public async Task A_Real_Payment_To_Savings_Replaces_The_Automatic_One_For_That_Month()
    {
        await using (var db = new CoinStackDbContext(_options))
        {
            AccountService.AddTransfer(db, _mainId, _savingsId, 500m, new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc),
                "Monthly savings 2026-07", TransactionConventions.AutoSource);
            await db.SaveChangesAsync();
        }

        var import = ImportService();
        var preview = await import.BuildPreviewAsync(Csv(ChequeNumber, ("2026/07/03", "Payment To Savings Holiday", -500m)), "cheque.csv");
        Assert.NotNull(preview!.Candidates.Single().ReplacesTransactionId);

        await import.ImportAsync(preview);

        await using var check = new CoinStackDbContext(_options);
        var transfer = await check.Transactions.SingleAsync();
        Assert.Equal(TransactionConventions.BankSource, transfer.Source);
        Assert.Equal(2500m, (await AccountService.GetBalancesAsync(check, default)).Single(b => b.Account.Id == _savingsId).Balance);
    }

    [Fact]
    public async Task An_Import_Can_Be_Moved_To_Another_Account()
    {
        await using (var db = new CoinStackDbContext(_options))
        {
            (await db.Accounts.SingleAsync(a => a.Id == _savingsId)).AccountNumber = null;
            await db.SaveChangesAsync();
        }

        var import = ImportService();
        var result = await import.ImportAsync((await import.BuildPreviewAsync(Csv(null, ("2026/07/03", "Interest", 12m)), "no-number.csv"))!);

        await import.MoveImportAsync(result.ImportId, _savingsId);

        await using var check = new CoinStackDbContext(_options);
        Assert.Equal(_savingsId, (await check.StatementImports.SingleAsync()).AccountId);
        Assert.Equal(_savingsId, (await check.Transactions.SingleAsync()).AccountId);
    }

    [Fact]
    public void An_Ofx_File_Gives_Its_Account_Number_And_Closing_Balance()
    {
        const string ofx = """
            <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS>
            <BANKACCTFROM><BANKID>250655<ACCTID>62000000001<ACCTTYPE>CHECKING</BANKACCTFROM>
            <BANKTRANLIST>
            <STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20260703<TRNAMT>-80.00<MEMO>POS Purchase Spar</STMTTRN>
            <STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20260705<TRNAMT>1000.00<MEMO>Salary</STMTTRN>
            </BANKTRANLIST>
            <LEDGERBAL><BALAMT>4920.00<DTASOF>20260705</LEDGERBAL>
            </STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
            """;

        var statement = FnbTransactionHistoryParser.ParseOfx(ofx);

        Assert.Equal("62000000001", statement.AccountNumber);
        Assert.Equal(4920m, statement.ClosingBalance);
        Assert.Equal(4000m, statement.OpeningBalance);
    }

    private BankStatementImportService ImportService()
        => new(new TestDbContextFactory(_options), new CompositeBankFeed([new FnbPdfBankFeed(), new FnbHistoryBankFeed()]));

    /// <summary>A transaction-history CSV, with an account column when <paramref name="account"/> is given; it opens at 5 000.</summary>
    private static MemoryStream Csv(string? account, params (string Date, string Description, decimal Amount)[] rows)
    {
        var balance = 5000m;
        var text = new StringBuilder(account is null ? "Date,Description,Amount,Balance\n" : "Account,Date,Description,Amount,Balance\n");
        foreach (var (date, description, amount) in rows)
        {
            balance += amount;
            var line = string.Create(CultureInfo.InvariantCulture, $"{date},{description},{amount:0.00},{balance:0.00}");
            text.Append(account is null ? line : $"{account},{line}").Append('\n');
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(text.ToString()));
    }
}
