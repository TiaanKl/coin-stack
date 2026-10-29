using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoinStack.Tests;

public sealed class DailyCatchUpTests : IAsyncLifetime
{
    private static readonly TimeZoneInfo Johannesburg = TimeZoneInfo.CreateCustomTimeZone("SAST", TimeSpan.FromHours(2), "SAST", "SAST");

    // 10:00 on 15 July 2026 in Johannesburg.
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero), Johannesburg);
    private readonly string _connectionString = $"Data Source=catch-up-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
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
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await DatabaseInitializer.InitializeAsync(_provider);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _keepAlive.DisposeAsync();
    }

    private IDailyCatchUpService CatchUp => _provider.GetRequiredService<IDailyCatchUpService>();

    [Fact]
    public async Task It_Runs_Once_Per_Local_Day_Even_After_A_Restart()
    {
        Assert.True(await CatchUp.RunIfDueAsync());
        Assert.False(await CatchUp.RunIfDueAsync());

        // A fresh instance, as after restarting the app, reads the last run from the database.
        var restarted = new DailyCatchUpService(_provider.GetRequiredService<IServiceScopeFactory>(), _clock);
        Assert.False(await restarted.RunIfDueAsync());

        // 23:30 the same day is still the same day; 00:30 is the next.
        _clock.UtcNow = new DateTimeOffset(2026, 7, 15, 21, 30, 0, TimeSpan.Zero);
        Assert.False(await CatchUp.RunIfDueAsync());
        _clock.UtcNow = new DateTimeOffset(2026, 7, 15, 22, 30, 0, TimeSpan.Zero);
        Assert.True(await CatchUp.RunIfDueAsync());
    }

    [Fact]
    public async Task Auto_Deductions_Are_Filled_In_For_Every_Period_Missed_But_None_Before_The_Expense_Existed()
    {
        await using (var db = await NewContextAsync())
        {
            db.AppSettings.Add(new AppSettings { MonthStartDay = 1 });
            db.Transactions.Add(new Transaction
            {
                Description = "Gym",
                Amount = 450m,
                Type = TransactionType.Expense,
                ExpenseKind = ExpenseKind.Mandatory,
                AutoDeduct = true,
                OccurredAtUtc = new DateTime(2026, 7, 10, 8, 0, 0)
            });
            await db.SaveChangesAsync();
        }

        Assert.True(await CatchUp.RunIfDueAsync());
        Assert.Empty(await AutoDeductionsAsync()); // July is the gym's own month

        // The app isn't opened again until mid-October.
        _clock.UtcNow = new DateTimeOffset(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
        Assert.True(await CatchUp.RunIfDueAsync());

        var applied = await AutoDeductionsAsync();
        Assert.Equal(
            [new DateTime(2026, 7, 31, 22, 0, 0), new DateTime(2026, 8, 31, 22, 0, 0), new DateTime(2026, 9, 30, 22, 0, 0)],
            applied.Select(t => t.OccurredAtUtc));
        Assert.All(applied, t => Assert.Equal(450m, t.Amount));
    }

    [Fact]
    public async Task The_Check_In_Is_Awarded_Once_A_Day_With_Streaks_Switched_Off()
    {
        await using (var db = await NewContextAsync())
        {
            db.AppSettings.Add(new AppSettings { EnableStreaks = false, EnableScoring = true });
            await db.SaveChangesAsync();
        }

        await using var scope = _provider.CreateAsyncScope();
        var gameLoop = scope.ServiceProvider.GetRequiredService<IGameLoopService>();
        await gameLoop.ProcessDailyCheckInAsync();
        await gameLoop.ProcessDailyCheckInAsync();

        await using var check = await NewContextAsync();
        Assert.Equal(1, await check.ScoreEvents.CountAsync(e => e.Reason == ScoreChangeReason.DailyCheckIn));
    }

    private async Task<List<Transaction>> AutoDeductionsAsync()
    {
        await using var db = await NewContextAsync();
        return await db.Transactions.Where(t => t.AutoDeductTemplateId != null).OrderBy(t => t.OccurredAtUtc).ToListAsync();
    }

    private async Task<CoinStackDbContext> NewContextAsync()
        => await _provider.GetRequiredService<IDbContextFactory<CoinStackDbContext>>().CreateDbContextAsync();
}
