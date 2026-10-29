using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoinStack.Tests;

/// <summary>Days follow the device's calendar: 00:30 in Johannesburg is already tomorrow, though UTC disagrees.</summary>
public sealed class LocalTimeTests : IAsyncLifetime
{
    private static readonly TimeZoneInfo Johannesburg = TimeZoneInfo.CreateCustomTimeZone("SAST", TimeSpan.FromHours(2), "SAST", "SAST");

    // 22:30 UTC on Wednesday 28 October is 00:30 on Thursday 29 October in Johannesburg.
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 28, 22, 30, 0, TimeSpan.Zero), Johannesburg);
    private readonly string _connectionString = $"Data Source=local-time-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
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
    public async Task A_Check_In_After_Local_Midnight_Starts_A_New_Day()
    {
        await using (var db = await NewContextAsync())
        {
            db.AppSettings.Add(new AppSettings { EnableStreaks = true, EnableScoring = true });
            // 23:30 on the 28th in Johannesburg: the same UTC date as now, but yesterday locally.
            db.Streaks.Add(new Streak { Type = StreakType.DailyCheckIn, CurrentCount = 3, BestCount = 3, LastIncrementedAtUtc = new DateTime(2026, 10, 28, 21, 30, 0) });
            await db.SaveChangesAsync();
        }

        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IGameLoopService>().ProcessDailyCheckInAsync();

        await using var check = await NewContextAsync();
        Assert.Equal(4, (await check.Streaks.SingleAsync(s => s.Type == StreakType.DailyCheckIn)).CurrentCount);
    }

    [Fact]
    public async Task Daily_Totals_Group_By_The_Local_Day()
    {
        await using (var db = await NewContextAsync())
        {
            db.Transactions.AddRange(
                new Transaction { Description = "Lunch", Amount = 80m, Type = TransactionType.Expense, OccurredAtUtc = new DateTime(2026, 10, 28, 12, 0, 0) },
                new Transaction { Description = "Late snack", Amount = 30m, Type = TransactionType.Expense, OccurredAtUtc = new DateTime(2026, 10, 28, 22, 10, 0) });
            await db.SaveChangesAsync();
        }

        await using var scope = _provider.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<ITransactionService>()
            .GetDailyNetForRangeAsync(_clock.StartOfDayUtc(new DateTime(2026, 10, 28)), _clock.StartOfDayUtc(new DateTime(2026, 10, 30)));

        Assert.Equal(2, rows.Count);
        Assert.Equal(new DailyNetSummary(new DateTime(2026, 10, 29), 0m, 30m), rows[0]);
        Assert.Equal(new DailyNetSummary(new DateTime(2026, 10, 28), 0m, 80m), rows[1]);
    }

    [Fact]
    public async Task Challenges_Run_From_Local_Midnight_And_Weeks_From_Local_Monday()
    {
        await using var scope = _provider.CreateAsyncScope();
        var challenges = await scope.ServiceProvider.GetRequiredService<IDailyChallengeService>().GetTodaysChallengesAsync();

        var daily = challenges.Where(c => c.Frequency == ChallengeFrequency.Daily).ToList();
        Assert.Equal(3, daily.Count);
        Assert.All(daily, c => Assert.Equal(new DateTime(2026, 10, 28, 22, 0, 0), c.AssignedDateUtc));
        Assert.All(daily, c => Assert.Equal(new DateTime(2026, 10, 29, 22, 0, 0), c.ExpiresAtUtc));

        var weekly = Assert.Single(challenges, c => c.Frequency == ChallengeFrequency.Weekly);
        Assert.Equal(new DateTime(2026, 10, 25, 22, 0, 0), weekly.AssignedDateUtc); // Monday 26 October, local
    }

    [Fact]
    public async Task Spending_Just_After_Local_Midnight_On_Payday_Belongs_To_The_New_Period()
    {
        await using (var db = await NewContextAsync())
        {
            db.Transactions.Add(new Transaction { Description = "Coffee", Amount = 35m, Type = TransactionType.Expense, OccurredAtUtc = new DateTime(2026, 10, 28, 22, 15, 0) });
            await db.SaveChangesAsync();
        }

        await using var scope = _provider.CreateAsyncScope();
        var spent = await scope.ServiceProvider.GetRequiredService<ITransactionService>()
            .GetExpenseTotalForBudgetPeriodAsync(29, _clock.GetUtcNow().UtcDateTime);

        Assert.Equal(35m, spent);
    }

    private async Task<CoinStackDbContext> NewContextAsync()
        => await _provider.GetRequiredService<IDbContextFactory<CoinStackDbContext>>().CreateDbContextAsync();
}

/// <summary>A clock that stands still until a test moves it.</summary>
internal sealed class FixedClock(DateTimeOffset utcNow, TimeZoneInfo zone) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public override TimeZoneInfo LocalTimeZone => zone;
}
