using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CoinStack.Services.Import;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoinStack.Tests;

public sealed class ForecastServiceTests : IAsyncLifetime
{
    private static readonly TimeZoneInfo Johannesburg = TimeZoneInfo.CreateCustomTimeZone("SAST", TimeSpan.FromHours(2), "SAST", "SAST");

    // 10:00 on Thursday 15 October 2026 in Johannesburg.
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 15, 8, 0, 0, TimeSpan.Zero), Johannesburg);
    private readonly string _connectionString = $"Data Source=forecast-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private SqliteConnection _keepAlive = null!;
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        await _keepAlive.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFinanceManagerData(_connectionString);
        services.AddFinanceManagerAppServices();
        services.AddSingleton<TimeProvider>(_clock);
        _provider = services.BuildServiceProvider();
        await DatabaseInitializer.InitializeAsync(_provider);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _keepAlive.DisposeAsync();
    }

    [Fact]
    public async Task Without_History_Salary_Lands_On_Payday_And_Debit_Orders_On_Their_Day()
    {
        await using (var db = await NewContextAsync())
        {
            db.AppSettings.Add(new AppSettings { MonthlyIncome = 20000m, MonthStartDay = 25 });
            db.Subscriptions.Add(new Subscription { Name = "Netflix", Cost = 199m, Cycle = SubscriptionCycle.Monthly, DebitOrderDay = 20, Status = SubscriptionStatus.Active });
            await db.SaveChangesAsync();
        }

        var forecast = await ForecastAsync(1000m);

        Assert.Equal(30, forecast.Days.Count);
        Assert.Equal(new DateTime(2026, 10, 16), forecast.Days[0].Date);
        Assert.Equal(20000m, forecast.MonthlyIncome);
        Assert.Equal(199m, forecast.MonthlyExpenses);

        Assert.Equal(0m, Day(forecast, 19).ExpensesToDate);
        Assert.Equal(199m, Day(forecast, 20).ExpensesToDate);
        Assert.Equal(0m, Day(forecast, 24).IncomeToDate);
        Assert.Equal(20000m, Day(forecast, 25).IncomeToDate);
        Assert.Equal(1000m + 20000m - 199m, forecast.EndingBalance);
    }

    [Fact]
    public async Task Thirty_Days_Match_The_Month_Bank_Import_Projects()
    {
        await using (var db = await NewContextAsync())
        {
            db.AppSettings.Add(new AppSettings { MonthStartDay = 25 });
            // R100 every day from 1 July to yesterday: July and August come to R3 100, September to R3 000.
            for (var day = new DateTime(2026, 7, 1); day < new DateTime(2026, 10, 15); day = day.AddDays(1))
            {
                db.Transactions.Add(new Transaction { Description = "Spaza", Amount = 100m, Type = TransactionType.Expense, OccurredAtUtc = day.AddHours(8) });
            }

            await db.SaveChangesAsync();
        }

        var forecast = await ForecastAsync(0m);
        await using var scope = _provider.CreateAsyncScope();
        var projection = await scope.ServiceProvider.GetRequiredService<IProjectionService>().BuildProjectionAsync();

        Assert.Equal(3100m, projection.ProjectedMonthlyExpenses);
        Assert.Equal(projection.ProjectedMonthlyExpenses, forecast.MonthlyExpenses);
        var expected = 3100m / ForecastService.DaysPerMonth * 30;
        Assert.InRange(forecast.Expenses, expected - 1m, expected + 1m);
    }

    [Fact]
    public async Task Spending_Follows_The_Weekdays_That_Usually_Cost_More()
    {
        await using (var db = await NewContextAsync())
        {
            db.AppSettings.Add(new AppSettings { MonthStartDay = 25 });
            // Only Saturdays cost anything: R700 every Saturday since July.
            for (var day = new DateTime(2026, 7, 4); day < new DateTime(2026, 10, 15); day = day.AddDays(7))
            {
                db.Transactions.Add(new Transaction { Description = "Braai", Amount = 700m, Type = TransactionType.Expense, OccurredAtUtc = day.AddHours(8) });
            }

            await db.SaveChangesAsync();
        }

        var forecast = await ForecastAsync(0m);

        var friday = Day(forecast, 16);
        var saturday = Day(forecast, 17);
        Assert.Equal(DayOfWeek.Saturday, saturday.Date.DayOfWeek);
        Assert.Equal(0m, friday.ExpensesToDate);
        Assert.True(saturday.ExpensesToDate > 0m);
    }

    private async Task<CashFlowForecast> ForecastAsync(decimal startingBalance)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IForecastService>().ForecastAsync(startingBalance);
    }

    private static ForecastDay Day(CashFlowForecast forecast, int dayOfOctober)
        => forecast.Days.Single(d => d.Date == new DateTime(2026, 10, dayOfOctober));

    private async Task<CoinStackDbContext> NewContextAsync()
        => await _provider.GetRequiredService<IDbContextFactory<CoinStackDbContext>>().CreateDbContextAsync();
}
