using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

public sealed class DebtInterestAccrualTests
{
    private static readonly DateTime Start = new(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void First_Run_Starts_The_Clock_Without_Back_Charging()
    {
        var debt = Loan(10_000m, 20m);
        debt.InterestAccruedThroughUtc = null;

        var added = DebtInterestAccrual.Accrue(debt, Start.AddMonths(6));

        Assert.Equal(0m, added);
        Assert.Equal(10_000m, debt.CurrentBalance);
        Assert.Equal(Start.AddMonths(6), debt.InterestAccruedThroughUtc);
    }

    [Fact]
    public void One_Month_Posts_One_Month_Of_Interest_And_Is_Idempotent()
    {
        var debt = Loan(10_000m, 20m);

        var added = DebtInterestAccrual.Accrue(debt, Start.AddMonths(1));
        var again = DebtInterestAccrual.Accrue(debt, Start.AddMonths(1).AddDays(3));

        Assert.Equal(166.67m, added);
        Assert.Equal(0m, again);
        Assert.Equal(10_166.67m, debt.CurrentBalance);
    }

    [Fact]
    public void Posting_Day_Moves_To_Month_End_In_Short_Months()
    {
        var debt = Loan(1_200m, 12m);
        debt.PaymentStartDateUtc = new DateTime(2027, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        debt.InterestAccruedThroughUtc = new DateTime(2027, 1, 31, 0, 0, 0, DateTimeKind.Utc);

        DebtInterestAccrual.Accrue(debt, new DateTime(2027, 2, 28, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2027, 2, 28), debt.InterestAccruedThroughUtc!.Value.Date);

        DebtInterestAccrual.Accrue(debt, new DateTime(2027, 3, 30, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2027, 2, 28), debt.InterestAccruedThroughUtc!.Value.Date); // 31 Mar not reached

        DebtInterestAccrual.Accrue(debt, new DateTime(2027, 3, 31, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2027, 3, 31), debt.InterestAccruedThroughUtc!.Value.Date);
    }

    [Fact]
    public void Monthly_Accrual_Plus_Payments_Matches_The_Amortisation_Schedule()
    {
        // R10 000 at 20% with R500 a month: the old code showed R4 000 left after a year.
        var debt = Loan(10_000m, 20m);

        for (var month = 1; month <= 12; month++)
        {
            DebtInterestAccrual.Accrue(debt, Start.AddMonths(month));
            debt.CurrentBalance -= 500m;
        }

        var schedule = FinancialEngine.BuildAmortisationSchedule(10_000m, 0.20m, 500m);
        Assert.Equal(schedule.Instalments[11].ClosingBalance, debt.CurrentBalance);
        Assert.InRange(debt.CurrentBalance, 5_612m, 5_613m);
    }

    [Fact]
    public void Paid_Off_Debt_Accrues_Nothing_But_Keeps_Its_Clock()
    {
        var debt = Loan(0m, 20m);

        var added = DebtInterestAccrual.Accrue(debt, Start.AddMonths(3));

        Assert.Equal(0m, added);
        Assert.Equal(Start.AddMonths(3), debt.InterestAccruedThroughUtc);
    }

    [Fact]
    public async Task Deleting_An_Overpayment_Restores_The_Original_Balance()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CoinStackDbContext>().UseSqlite(connection).Options;

        int debtId;
        await using (var setupDb = new CoinStackDbContext(options))
        {
            await setupDb.Database.EnsureCreatedAsync();
            var debt = new DebtAccount { Name = "Store card", TotalAmount = 1_000m, CurrentBalance = 200m, MonthlyPaymentAmount = 100m };
            setupDb.DebtAccounts.Add(debt);
            await setupDb.SaveChangesAsync();
            debtId = debt.Id;
        }

        var service = new TransactionService(new TestDbContextFactory(options), new FakeGameLoopService());

        var payment = await service.CreateAsync(new Transaction
        {
            Description = "Settle store card",
            Amount = 500m,
            Type = TransactionType.Expense,
            DebtAccountId = debtId,
            OccurredAtUtc = DateTime.UtcNow
        });

        await using (var midDb = new CoinStackDbContext(options))
        {
            // Overpaid by R300: kept as a credit instead of being clamped to zero.
            Assert.Equal(-300m, (await midDb.DebtAccounts.SingleAsync()).CurrentBalance);
        }

        await service.DeleteAsync(payment.Id);

        await using var verifyDb = new CoinStackDbContext(options);
        Assert.Equal(200m, (await verifyDb.DebtAccounts.SingleAsync()).CurrentBalance);
    }

    private static DebtAccount Loan(decimal balance, decimal ratePercent) => new()
    {
        Name = "Loan",
        TotalAmount = 10_000m,
        CurrentBalance = balance,
        InterestRatePercent = ratePercent,
        MonthlyPaymentAmount = 500m,
        PaymentStartDateUtc = Start,
        InterestAccruedThroughUtc = Start
    };

    [Theory]
    [InlineData(25, 1, "2026-11-25")]
    [InlineData(29, 1, "2026-10-29")]
    [InlineData(31, 1, "2026-10-31")]
    [InlineData(31, 2, "2026-11-30")]
    [InlineData(31, 5, "2027-02-28")]
    public void Debit_Order_Dates_Fall_On_The_Debit_Day(int debitDay, int paymentNumber, string expected)
    {
        var from = new DateTime(2026, 10, 29, 0, 0, 0, DateTimeKind.Utc);

        var date = DebtInterestAccrual.DebitOrderDate(from, debitDay, paymentNumber);

        Assert.Equal(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture).Date, date.Date);
    }
}
