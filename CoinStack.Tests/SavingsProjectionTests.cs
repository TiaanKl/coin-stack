using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

/// <summary>
/// Verifies that <see cref="SavingsService"/> projections agree with <see cref="FinancialEngine"/>
/// and that the stored interest rate is interpreted as a fraction exactly once.
/// </summary>
public sealed class SavingsProjectionTests
{
    /// <summary>
    /// Creates an isolated in-memory database seeded with the supplied settings and opening balance.
    /// </summary>
    /// <param name="settings">The application settings to persist.</param>
    /// <param name="openingBalance">The opening savings balance.</param>
    /// <returns>The live connection and a service bound to it.</returns>
    private static async Task<(SqliteConnection Connection, SavingsService Service)> CreateServiceAsync(
        AppSettings settings,
        decimal openingBalance)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CoinStackDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new CoinStackDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.AppSettings.Add(settings);
            db.Accounts.Add(new Account
            {
                Name = "Savings",
                Type = AccountType.Savings,
                Balance = openingBalance,
                BalanceAsOfUtc = DateTime.UtcNow.AddDays(-1)
            });
            await db.SaveChangesAsync();
        }

        return (connection, new SavingsService(new TestDbContextFactory(options)));
    }

    [Fact]
    public async Task GetProjectionsAsync_FinalMonth_MatchesFinancialEngine()
    {
        var settings = new AppSettings
        {
            MonthlyIncome = 0m,
            SavingsIsPercent = false,
            MonthlySavingsAmount = 500m,
            SavingsInterestRate = 0.07m,
            SavingsInterestIsYearly = true
        };

        var (connection, service) = await CreateServiceAsync(settings, 10000m);

        try
        {
            var projections = await service.GetProjectionsAsync(12, includeInterest: true);
            var expected = FinancialEngine.CalculateSavingsProjection(10000m, 500m, 0.07m, 12);

            Assert.Equal(12, projections.Count);
            Assert.Equal(expected.FutureValue, projections[^1].Projected);
            Assert.Equal(expected.TotalContributions, projections[^1].Contributions);
            Assert.Equal(expected.TotalInterestEarned, projections[^1].InterestEarned);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetProjectionsAsync_DoesNotDivideStoredFractionByOneHundredAgain()
    {
        // 7% stored as 0.07. The previous implementation divided by 100 a second time, producing
        // an effective 0.07% and silently under-reporting growth.
        var settings = new AppSettings
        {
            MonthlyIncome = 0m,
            SavingsIsPercent = false,
            MonthlySavingsAmount = 500m,
            SavingsInterestRate = 0.07m,
            SavingsInterestIsYearly = true
        };

        var (connection, service) = await CreateServiceAsync(settings, 10000m);

        try
        {
            var withInterest = await service.GetProjectionsAsync(12, includeInterest: true);
            var withoutInterest = await service.GetProjectionsAsync(12, includeInterest: false);

            var interestEarned = withInterest[^1].Projected - withoutInterest[^1].Projected;

            // A correct 7% annual rate on this profile earns roughly R900 in year one.
            // The old double-division bug yielded under R10.
            Assert.True(
                interestEarned > 800m,
                $"Expected roughly 900 of interest for a 7% rate, but got {interestEarned}.");
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetProjectionsAsync_MonthlyQuotedRate_IsAnnualisedNotTreatedAsAnnual()
    {
        // 0.5% per month stored as 0.005 must behave as a 6% annual nominal rate.
        var monthlySettings = new AppSettings
        {
            MonthlyIncome = 0m,
            MonthlySavingsAmount = 0m,
            SavingsInterestRate = 0.005m,
            SavingsInterestIsYearly = false
        };

        var (connection, service) = await CreateServiceAsync(monthlySettings, 10000m);

        try
        {
            var projections = await service.GetProjectionsAsync(12, includeInterest: true);
            var expected = FinancialEngine.CalculateSavingsProjection(10000m, 0m, 0.06m, 12);

            Assert.Equal(expected.FutureValue, projections[^1].Projected);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetProjectionsAsync_WithInterestDisabled_ReturnsContributionsOnly()
    {
        var settings = new AppSettings
        {
            MonthlyIncome = 0m,
            MonthlySavingsAmount = 250m,
            SavingsInterestRate = 0.09m,
            SavingsInterestIsYearly = true
        };

        var (connection, service) = await CreateServiceAsync(settings, 1000m);

        try
        {
            var projections = await service.GetProjectionsAsync(6, includeInterest: false);

            Assert.Equal(2500m, projections[^1].Projected);
            Assert.Equal(0m, projections[^1].InterestEarned);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetProjectionsAsync_IsMonotonicallyIncreasing()
    {
        var settings = new AppSettings
        {
            MonthlyIncome = 0m,
            MonthlySavingsAmount = 300m,
            SavingsInterestRate = 0.08m,
            SavingsInterestIsYearly = true
        };

        var (connection, service) = await CreateServiceAsync(settings, 5000m);

        try
        {
            var projections = await service.GetProjectionsAsync(24, includeInterest: true);

            for (var i = 1; i < projections.Count; i++)
            {
                Assert.True(
                    projections[i].Projected > projections[i - 1].Projected,
                    $"Month {projections[i].Month} did not grow relative to {projections[i - 1].Month}.");
            }
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetProjectionsAsync_PercentageBasedSavings_UsesIncomeShare()
    {
        var settings = new AppSettings
        {
            MonthlyIncome = 20000m,
            SavingsIsPercent = true,
            MonthlySavingsPercent = 10m,
            SavingsInterestRate = null
        };

        var (connection, service) = await CreateServiceAsync(settings, 0m);

        try
        {
            var projections = await service.GetProjectionsAsync(3, includeInterest: true);

            // 10% of 20 000 = 2 000 per month, no interest configured.
            Assert.Equal(6000m, projections[^1].Projected);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Projections_And_Monthly_Interest_Start_From_Available_After_A_Withdrawal()
    {
        var settings = new AppSettings
        {
            MonthlyIncome = 0m,
            SavingsIsPercent = false,
            MonthlySavingsAmount = 500m,
            SavingsInterestRate = 0.12m,
            SavingsInterestIsYearly = true
        };

        var (connection, service) = await CreateServiceAsync(settings, 10000m);

        try
        {
            // R6 000 of the R10 000 moves to the main account.
            await service.WithdrawForEmergencyAsync(6000m, "Car repair");

            var projections = await service.GetProjectionsAsync(12, includeInterest: true);
            var expected = FinancialEngine.CalculateSavingsProjection(4000m, 500m, 0.12m, 12);
            Assert.Equal(expected.FutureValue, projections[^1].Projected);

            var summary = await service.CalculateMonthAsync();
            Assert.NotNull(summary);
            // 1% of (R4 000 + R500), not of (R10 000 + R500).
            Assert.Equal(45m, summary.Interest);
            Assert.Equal(4545m, summary.RunningTotal);

            // The month's R500 came out of the main account; the interest was added to savings.
            var state = await service.GetSnapshotAsync();
            Assert.Equal(4545m, state.SavingsBalance);
            Assert.Equal(4545m, state.Available);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}
