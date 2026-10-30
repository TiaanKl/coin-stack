using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace CoinStack.Tests;

/// <summary>
/// Upgrading a database from before accounts existed: the single bank balance becomes the main account,
/// every transaction lands on it, and the money shown before and after is the same.
/// </summary>
public sealed class AccountsMigrationTests : IAsyncLifetime
{
    private const string BeforeAccounts = "20261029174011_MerchantRules";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<CoinStackDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<CoinStackDbContext>().UseSqlite(_connection).Options;
        await using var db = new CoinStackDbContext(_options);
        await db.GetService<IMigrator>().MigrateAsync(BeforeAccounts);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task The_Bank_Balance_Becomes_The_Main_Account_And_Shows_The_Same_Money()
    {
        await SeedOldDatabaseAsync(bankBalanceAsOf: "'2026-10-01 12:00:00'");

        await UpgradeAsync();

        await using var db = new CoinStackDbContext(_options);
        var main = await db.Accounts.SingleAsync();
        Assert.Equal("Main account", main.Name);
        Assert.Equal(AccountType.Cheque, main.Type);
        Assert.True(main.IsDefault);
        Assert.Equal(5000m, main.Balance);
        Assert.Equal(new DateTime(2026, 10, 1, 12, 0, 0), main.BalanceAsOfUtc);
        Assert.Equal("62782338050", main.AccountNumber);

        Assert.All(await db.Transactions.ToListAsync(), t => Assert.Equal(main.Id, t.AccountId));
        Assert.Equal(main.Id, (await db.StatementImports.SingleAsync()).AccountId);

        // As before: the statement's 5000, plus the salary, minus the rent. The row before the balance and the
        // old transfer, which has nowhere to go, change nothing.
        Assert.Equal(17000m, await AccountService.GetSpendableBalanceAsync(db, CancellationToken.None));
    }

    [Fact]
    public async Task A_Balance_Never_Entered_Stays_Unset()
    {
        await SeedOldDatabaseAsync(bankBalanceAsOf: "NULL");

        await UpgradeAsync();

        await using var db = new CoinStackDbContext(_options);
        var main = await db.Accounts.SingleAsync();
        Assert.Null(main.BalanceAsOfUtc);
        Assert.Equal(5000m, await AccountService.GetSpendableBalanceAsync(db, CancellationToken.None));
    }

    [Fact]
    public async Task Links_From_Scores_Reflections_And_Journal_Entries_Survive_The_Rebuild()
    {
        await SeedOldDatabaseAsync(bankBalanceAsOf: "'2026-10-01 12:00:00'");

        await UpgradeAsync();

        Assert.Equal(2L, await ScalarAsync("SELECT \"TransactionId\" FROM \"ScoreEvents\""));
        Assert.Equal(3L, await ScalarAsync("SELECT \"TransactionId\" FROM \"Reflections\""));
        Assert.Equal(3L, await ScalarAsync("SELECT \"TransactionId\" FROM \"CbtJournalEntries\""));
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM pragma_foreign_key_check"));
    }

    [Fact]
    public async Task Going_Back_And_Upgrading_Again_Keeps_The_Balance()
    {
        await SeedOldDatabaseAsync(bankBalanceAsOf: "'2026-10-01 12:00:00'");
        await UpgradeAsync();

        await using (var db = new CoinStackDbContext(_options))
        {
            await db.GetService<IMigrator>().MigrateAsync(BeforeAccounts);
        }

        Assert.Equal("5000.0", await ScalarAsync("SELECT \"CurrentBankBalance\" FROM \"AppSettings\""));

        await UpgradeAsync();

        await using var again = new CoinStackDbContext(_options);
        Assert.Single(await again.Accounts.ToListAsync());
        Assert.Equal(17000m, await AccountService.GetSpendableBalanceAsync(again, CancellationToken.None));
    }

    private async Task UpgradeAsync()
    {
        await using var db = new CoinStackDbContext(_options);
        await db.Database.MigrateAsync();
    }

    /// <summary>The tables as they were before accounts: one bank balance in settings, a statement, and rows.</summary>
    private async Task SeedOldDatabaseAsync(string bankBalanceAsOf)
    {
        const string stamps = "'2026-09-01 00:00:00', '2026-09-01 00:00:00'";
        await ExecuteAsync(
            $"""
             INSERT INTO "AppSettings" ("Currency", "MonthStartDay", "MonthlyIncome", "CurrentBankBalance", "BankBalanceAsOfUtc",
                 "EnableScoring", "EnableStreaks", "EnableToast", "EnableSounds", "EnableReflections", "LargeExpenseThreshold",
                 "SavingsIsPercent", "MonthlySavingsAmount", "MonthlySavingsPercent", "SavingsInterestIsYearly",
                 "ShowReserveAwareBudget", "EnableEmergencyFallback", "CreatedAtUtc", "UpdatedAtUtc")
             VALUES ('ZAR', 25, '20000.0', '5000.0', {bankBalanceAsOf}, 1, 1, 1, 1, 1, 50, 0, '0.0', '0.0', 1, 0, 1, {stamps});

             INSERT INTO "StatementImports" ("Id", "FileName", "BankFormat", "AccountNumber", "PeriodEndUtc", "RowsParsed",
                 "TransactionsImported", "DuplicatesSkipped", "TransfersSkipped", "Status", "CreatedAtUtc", "UpdatedAtUtc")
             VALUES (1, 'statement.pdf', 'FNB', '62782338050', '2026-09-30 00:00:00', 1, 1, 0, 0, 'Imported', {stamps});

             INSERT INTO "Transactions" ("Id", "OccurredAtUtc", "Amount", "Type", "Description", "ExpenseKind", "AutoDeduct",
                 "IsImpulse", "Source", "StatementImportId", "CreatedAtUtc", "UpdatedAtUtc")
             VALUES
                 (1, '2026-09-30 10:00:00', '999.0', 'Expense', 'Before the balance', 'Discretionary', 0, 0, 'FNB', 1, {stamps}),
                 (2, '2026-10-03 10:00:00', '20000.0', 'Income', 'Salary', 'Discretionary', 0, 0, 'Manual', NULL, {stamps}),
                 (3, '2026-10-04 10:00:00', '8000.0', 'Expense', 'Rent', 'Discretionary', 0, 0, 'Manual', NULL, {stamps}),
                 (4, '2026-10-05 10:00:00', '3000.0', 'Transfer', 'To savings', 'Discretionary', 0, 0, 'Manual', NULL, {stamps});

             INSERT INTO "ScoreEvents" ("Points", "Reason", "Description", "TransactionId", "CreatedAtUtc", "UpdatedAtUtc")
             VALUES (5, 'UnderBudget', 'Stayed under budget', 2, {stamps});

             INSERT INTO "Reflections" ("Trigger", "Prompt", "MoodBefore", "MoodAfter", "IsCompleted", "TransactionId", "CreatedAtUtc", "UpdatedAtUtc")
             VALUES ('LargeExpense', 'Why?', 3, 3, 0, 3, {stamps});

             INSERT INTO "CbtJournalEntries" ("Situation", "AutomaticThought", "Emotion", "EmotionIntensity", "RationalResponse",
                 "MoodBefore", "MoodAfter", "TransactionId", "CreatedAtUtc", "UpdatedAtUtc")
             VALUES ('Rent', 'Thought', 'Anxious', 5, 'Response', 3, 4, 3, {stamps});
             """);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
}
