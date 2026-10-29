using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CoinStack.Services.Import;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

public sealed class MonthlyTotalsTests
{
    [Fact]
    public void Three_Monthly_Salaries_Are_One_Salary_A_Month()
    {
        var payments = new[] { Pay(2026, 7, 25, 30000m), Pay(2026, 8, 25, 30500m), Pay(2026, 9, 25, 30739.39m) };

        Assert.Equal(30500m, MonthlyTotals.Typical(payments));
    }

    [Fact]
    public void Fortnightly_Pay_Adds_Up_To_The_Month()
    {
        var payments = new[]
        {
            Pay(2026, 7, 10, 15000m), Pay(2026, 7, 24, 15000m),
            Pay(2026, 8, 7, 15000m), Pay(2026, 8, 21, 15000m),
            Pay(2026, 9, 4, 15000m), Pay(2026, 9, 18, 15000m)
        };

        Assert.Equal(30000m, MonthlyTotals.Typical(payments));
    }

    [Fact]
    public void A_Bonus_Month_Does_Not_Lift_The_Usual_Salary()
    {
        var payments = new[] { Pay(2026, 10, 25, 30000m), Pay(2026, 11, 25, 30000m), Pay(2026, 12, 15, 30000m), Pay(2026, 12, 25, 30000m) };

        Assert.Equal(30000m, MonthlyTotals.Typical(payments));
        Assert.Equal(0m, MonthlyTotals.Typical([]));
    }

    [Theory]
    [InlineData("2026-07-01", "2026-09-30", 3)]
    [InlineData("2026-07-18", "2026-10-17", 3)]
    [InlineData("2026-10-01", "2026-10-31", 1)]
    [InlineData("2026-10-12", "2026-10-12", 1)]
    [InlineData("2026-01-01", "2026-12-31", 12)]
    public void Months_Covered_Rounds_To_The_Nearest_Month(string first, string last, int expected)
    {
        Assert.Equal(expected, MonthlyTotals.MonthsCovered(DateTime.Parse(first), DateTime.Parse(last)));
    }

    [Fact]
    public void A_Three_Month_Statement_Shows_Income_Per_Month()
    {
        var preview = new StatementImportPreview { PeriodStartUtc = new DateTime(2026, 7, 1), PeriodEndUtc = new DateTime(2026, 9, 30) };
        preview.Candidates.Add(new ImportCandidate { Type = TransactionType.Income, Amount = 91239.39m, DateUtc = new DateTime(2026, 9, 25) });

        Assert.Equal(3, preview.MonthsCovered);
        Assert.Equal(30413.13m, preview.PerMonth(preview.TotalIncome));
    }

    [Fact]
    public async Task Importing_Fortnightly_Salary_Sets_The_Monthly_Income_Not_One_Payment()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (connection)
        {
            var options = new DbContextOptionsBuilder<CoinStackDbContext>().UseSqlite(connection).Options;
            await using (var db = new CoinStackDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var service = new BankStatementImportService(new TestDbContextFactory(options), new FnbPdfBankFeed());
            var preview = new StatementImportPreview { BankFormat = "FNB", FileName = "fortnightly.pdf" };
            var paydays = new[] { new DateTime(2026, 8, 7), new DateTime(2026, 8, 21), new DateTime(2026, 9, 4), new DateTime(2026, 9, 18) };
            foreach (var day in paydays)
            {
                preview.Candidates.Add(new ImportCandidate
                {
                    DateUtc = day,
                    Amount = 12500m,
                    Type = TransactionType.Income,
                    Description = "SALARY ACME",
                    MerchantName = "Acme",
                    NormalizedKey = "ACME",
                    CategoryName = "Salary",
                    Fingerprint = $"fp-{day:yyyyMMdd}"
                });
            }

            var result = await service.ImportAsync(preview);

            Assert.Equal(25000m, result.DetectedMonthlySalary);
            await using var check = new CoinStackDbContext(options);
            Assert.Equal(25000m, (await check.AppSettings.SingleAsync()).MonthlyIncome);
        }
    }

    private static (DateTime Date, decimal Amount) Pay(int year, int month, int day, decimal amount) => (new DateTime(year, month, day), amount);
}
