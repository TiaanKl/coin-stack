using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Mobile.Services;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoinStack.Tests;

/// <summary>The mobile app now runs on the CoinStack.Core services; these check the wiring and the shaping.</summary>
public sealed class MobileFinanceServiceTests : IAsyncLifetime
{
    private readonly string _connectionString = $"Data Source=mobile-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private SqliteConnection _keepAlive = null!;
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        // A shared in-memory database lives as long as one connection to it stays open.
        _keepAlive = new SqliteConnection(_connectionString);
        await _keepAlive.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFinanceManagerData(_connectionString);
        services.AddFinanceManagerAppServices();
        services.AddSingleton<IMobileFinanceService, MobileFinanceService>();

        // The same checks a strict host runs: every service resolves and no singleton holds a scoped one.
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await DatabaseInitializer.InitializeAsync(_provider);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _keepAlive.DisposeAsync();
    }

    private IMobileFinanceService Mobile => _provider.GetRequiredService<IMobileFinanceService>();

    [Fact]
    public async Task Transactions_Go_Through_The_Shared_Game_Loop()
    {
        await Mobile.AddTransactionAsync(500m, TransactionType.Income, "Freelance");
        await Mobile.AddTransactionAsync(120m, TransactionType.Expense, "Groceries");

        var dashboard = await Mobile.GetDashboardSnapshotAsync();

        Assert.Equal(500m, dashboard.TotalIncome);
        Assert.Equal(120m, dashboard.TotalExpense);
        Assert.Equal(380m, dashboard.NetSaved);
        Assert.Equal(2, dashboard.RecentTransactions.Count);
        Assert.Equal("Groceries", dashboard.RecentTransactions[0].Description);
    }

    [Fact]
    public async Task Goal_Contributions_Move_Money_Out_Of_Savings()
    {
        await using (var db = await NewContextAsync())
        {
            db.SavingsState.Add(new SavingsState { Total = 1000m, Available = 1000m });
            await db.SaveChangesAsync();
        }

        await Mobile.AddGoalAsync("Holiday", 500m);
        var goalId = (await Mobile.GetGoalsAsync()).Single().Id;

        var result = await Mobile.AddGoalContributionAsync(goalId, 800m);

        Assert.True(result.Succeeded);
        Assert.Equal(500m, result.Moved);
        Assert.Equal(300m, result.NotMoved);
        var savings = await Mobile.GetSavingsSnapshotAsync();
        Assert.Equal(500m, savings.State.Available);
        Assert.Equal(500m, savings.State.Reserved);
    }

    [Fact]
    public async Task Income_And_Reports_Use_The_Pay_Period()
    {
        var settings = await Mobile.GetSettingsAsync();
        settings.MonthStartDay = 31;
        await Mobile.SaveSettingsAsync(settings);

        var period = BudgetPeriod.Containing(31, DateTime.UtcNow);
        await using (var db = await NewContextAsync())
        {
            db.Transactions.AddRange(
                new Transaction { Description = "Salary", Amount = 20000m, Type = TransactionType.Income, OccurredAtUtc = period.StartUtc.AddHours(9) },
                new Transaction { Description = "Last salary", Amount = 19000m, Type = TransactionType.Income, OccurredAtUtc = period.Previous.StartUtc.AddHours(9) });
            await db.SaveChangesAsync();
        }

        var income = await Mobile.GetIncomeSnapshotAsync();
        var report = await Mobile.GetReportSnapshotAsync();

        Assert.Equal(period, income.Period);
        Assert.Equal(20000m, income.PeriodToDate);
        Assert.Equal(20000m, report.PeriodIncome);
        Assert.Equal(2, income.RecentDeposits.Count);
    }

    [Fact]
    public async Task Startup_Seeds_The_Same_Data_As_The_Other_Apps()
    {
        Assert.Contains(await Mobile.GetCategoriesAsync(), c => c.Name == "Groceries");
        Assert.NotEmpty(await Mobile.GetAchievementsAsync());
        Assert.Equal(1, (await Mobile.GetLevelInfoAsync()).Level);
    }

    private async Task<CoinStackDbContext> NewContextAsync()
        => await _provider.GetRequiredService<IDbContextFactory<CoinStackDbContext>>().CreateDbContextAsync();
}
