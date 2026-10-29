using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CoinStack.Services.Import;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

public sealed class BudgetPeriodTests
{
    [Theory]
    [InlineData(25, "2026-10-29", "2026-10-25", "2026-11-25")]
    [InlineData(25, "2026-10-10", "2026-09-25", "2026-10-25")]
    [InlineData(25, "2027-01-05", "2026-12-25", "2027-01-25")]
    [InlineData(1, "2026-10-01", "2026-10-01", "2026-11-01")]
    [InlineData(31, "2026-02-15", "2026-01-31", "2026-02-28")]
    [InlineData(31, "2026-02-28", "2026-02-28", "2026-03-31")]
    [InlineData(31, "2026-04-30", "2026-04-30", "2026-05-31")]
    [InlineData(30, "2028-02-29", "2028-02-29", "2028-03-30")]
    [InlineData(29, "2027-03-01", "2027-02-28", "2027-03-29")]
    public void The_Period_Runs_From_Payday_To_The_Next_Payday(int payday, string now, string start, string end)
    {
        var period = BudgetPeriod.Containing(payday, DateTime.Parse(now), TimeZoneInfo.Utc);

        Assert.Equal(DateTime.Parse(start), period.StartUtc);
        Assert.Equal(DateTime.Parse(end), period.EndUtc);
        Assert.Equal(DateTimeKind.Utc, period.StartUtc.Kind);
    }

    [Fact]
    public void Payday_On_The_31st_Does_Not_Drift_Across_The_Year()
    {
        var period = BudgetPeriod.Containing(31, new DateTime(2026, 1, 31), TimeZoneInfo.Utc);

        for (var i = 0; i < 12; i++)
        {
            var month = period.FirstDay;
            Assert.Equal(DateTime.DaysInMonth(month.Year, month.Month), month.Day);
            Assert.Equal(period, period.Next.Previous);
            period = period.Next;
        }
    }

    [Fact]
    public void The_End_Belongs_To_The_Next_Period()
    {
        var period = BudgetPeriod.Containing(25, new DateTime(2026, 10, 29), TimeZoneInfo.Utc);

        Assert.True(period.Contains(new DateTime(2026, 10, 25)));
        Assert.True(period.Contains(new DateTime(2026, 11, 24, 23, 59, 59)));
        Assert.False(period.Contains(new DateTime(2026, 11, 25)));
        Assert.Equal(new DateTime(2026, 11, 24), period.LastDay);
    }

    [Fact]
    public void The_Period_Starts_At_Local_Midnight_On_Payday()
    {
        var johannesburg = TimeZoneInfo.CreateCustomTimeZone("SAST", TimeSpan.FromHours(2), "SAST", "SAST");

        // 01:30 on 25 October in Johannesburg is still 24 October in UTC.
        var period = BudgetPeriod.Containing(25, new DateTime(2026, 10, 24, 23, 30, 0, DateTimeKind.Utc), johannesburg);

        Assert.Equal(new DateTime(2026, 10, 25), period.FirstDay);
        Assert.Equal(new DateTime(2026, 11, 24), period.LastDay);
        Assert.Equal(new DateTime(2026, 10, 24, 22, 0, 0), period.StartUtc);
        Assert.Equal(new DateTime(2026, 11, 24, 22, 0, 0), period.EndUtc);
        Assert.True(period.Contains(new DateTime(2026, 10, 24, 22, 30, 0)));
        Assert.Equal(new DateTime(2026, 9, 25), period.Previous.FirstDay);
    }

    [Fact]
    public void A_Day_Starts_At_Its_First_Real_Minute_When_Clocks_Jump_At_Midnight()
    {
        // A zone that moves its clocks from 00:00 to 01:00 on the first Sunday of September.
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 0, 0, 0), 9, 1, DayOfWeek.Sunday),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 0, 0, 0), 4, 1, DayOfWeek.Sunday));
        var zone = TimeZoneInfo.CreateCustomTimeZone("Jump", TimeSpan.FromHours(-4), "Jump", "Jump", "Jump DST", [rule]);

        var start = LocalCalendar.StartOfDayUtc(zone, new DateTime(2026, 9, 6));

        Assert.Equal(new DateTime(2026, 9, 6, 4, 0, 0), start);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(32)]
    public void An_Invalid_Payday_Means_The_First(int payday)
    {
        Assert.Equal(1, BudgetPeriod.NormalizePayday(payday));
        Assert.Equal(new DateTime(2026, 10, 1), BudgetPeriod.Containing(payday, new DateTime(2026, 10, 29), TimeZoneInfo.Utc).StartUtc);
    }

    [Theory]
    [InlineData("2026-03-31,2026-04-30,2026-05-31", 31)]
    [InlineData("2026-03-30,2026-04-30,2026-05-30", 30)]
    [InlineData("2026-02-28,2026-03-28", 28)]
    [InlineData("2026-03-25,2026-04-24,2026-05-25", 25)]
    [InlineData("2026-03-26,2026-04-25", 25)]
    [InlineData("", 0)]
    public void Payday_Is_Detected_From_Salary_Dates(string dates, int expected)
    {
        var salaryDates = dates.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(DateTime.Parse);

        Assert.Equal(expected, BudgetPeriod.DetectPayday(salaryDates));
    }

    [Fact]
    public async Task Importing_Salary_Paid_On_The_Last_Day_Sets_Payday_31()
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
            var preview = new StatementImportPreview { BankFormat = "FNB", FileName = "salary.pdf" };
            preview.Candidates.Add(Salary(new DateTime(2026, 4, 30), "fp-april"));
            preview.Candidates.Add(Salary(new DateTime(2026, 5, 31), "fp-may"));

            await service.ImportAsync(preview);

            await using var check = new CoinStackDbContext(options);
            Assert.Equal(31, (await check.AppSettings.SingleAsync()).MonthStartDay);
        }
    }

    private static ImportCandidate Salary(DateTime date, string fingerprint) => new()
    {
        DateUtc = DateTime.SpecifyKind(date, DateTimeKind.Utc),
        Amount = 25000m,
        Type = TransactionType.Income,
        Description = "SALARY ACME",
        MerchantName = "Acme",
        NormalizedKey = "ACME",
        CategoryName = "Salary",
        Fingerprint = fingerprint
    };
}
