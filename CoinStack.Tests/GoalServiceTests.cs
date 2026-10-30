using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

public sealed class GoalServiceTests
{
    [Fact]
    public async Task Contribution_Moves_Money_Out_Of_Free_Savings_Into_The_Goal()
    {
        var (connection, options) = await CreateDatabaseAsync(savingsAvailable: 5000m);
        await using (connection)
        {
            var service = new GoalService(new TestDbContextFactory(options));
            var goalId = await AddGoalAsync(options, target: 3000m);

            var result = await service.ContributeAsync(goalId, 1000m);

            Assert.True(result.Succeeded);
            Assert.Equal(1000m, result.Moved);
            var (goal, savings) = await LoadAsync(options, goalId);
            Assert.Equal(1000m, goal.CurrentAmount);
            Assert.Equal(4000m, savings.Available);
            Assert.Equal(1000m, savings.Reserved);
        }
    }

    [Fact]
    public async Task Overfunding_Only_Moves_What_The_Goal_Needs_And_Says_So()
    {
        var (connection, options) = await CreateDatabaseAsync(savingsAvailable: 5000m);
        await using (connection)
        {
            var service = new GoalService(new TestDbContextFactory(options));
            var goalId = await AddGoalAsync(options, target: 3000m, current: 2500m);

            var result = await service.ContributeAsync(goalId, 1000m);

            Assert.Equal(500m, result.Moved);
            Assert.Equal(500m, result.NotMoved);
            Assert.True(result.GoalCompleted);
            var (goal, savings) = await LoadAsync(options, goalId);
            Assert.Equal(GoalStatus.Completed, goal.Status);
            Assert.Equal(4500m, savings.Available);

            var again = await service.ContributeAsync(goalId, 100m);
            Assert.False(again.Succeeded);
        }
    }

    [Fact]
    public async Task Nothing_Moves_When_Savings_Cannot_Cover_It()
    {
        var (connection, options) = await CreateDatabaseAsync(savingsAvailable: 200m);
        await using (connection)
        {
            var service = new GoalService(new TestDbContextFactory(options));
            var goalId = await AddGoalAsync(options, target: 3000m);

            var result = await service.ContributeAsync(goalId, 1000m);

            Assert.False(result.Succeeded);
            var (goal, savings) = await LoadAsync(options, goalId);
            Assert.Equal(0m, goal.CurrentAmount);
            Assert.Equal(200m, savings.Available);
        }
    }

    [Fact]
    public async Task Withdraw_Abandon_And_Delete_Put_The_Money_Back_In_Savings()
    {
        var (connection, options) = await CreateDatabaseAsync(savingsAvailable: 5000m);
        await using (connection)
        {
            var service = new GoalService(new TestDbContextFactory(options));
            var goalId = await AddGoalAsync(options, target: 3000m);
            await service.ContributeAsync(goalId, 2000m);

            await service.WithdrawAsync(goalId, 500m);
            var (afterWithdraw, savingsAfterWithdraw) = await LoadAsync(options, goalId);
            Assert.Equal(1500m, afterWithdraw.CurrentAmount);
            Assert.Equal(3500m, savingsAfterWithdraw.Available);
            Assert.Equal(1500m, savingsAfterWithdraw.Reserved);

            await service.AbandonAsync(goalId);
            var (abandoned, savingsAfterAbandon) = await LoadAsync(options, goalId);
            Assert.Equal(GoalStatus.Abandoned, abandoned.Status);
            Assert.Equal(0m, abandoned.CurrentAmount);
            Assert.Equal(5000m, savingsAfterAbandon.Available);
            Assert.Equal(0m, savingsAfterAbandon.Reserved);

            var secondId = await AddGoalAsync(options, target: 1000m);
            await service.ContributeAsync(secondId, 400m);
            await using (var db = new CoinStackDbContext(options))
            {
                db.Buckets.Add(new Bucket { Name = "Holiday fund", IsSavings = true, GoalId = secondId });
                await db.SaveChangesAsync();
            }

            await service.DeleteAsync(secondId);

            await using var check = new CoinStackDbContext(options);
            Assert.Null(await check.Goals.FindAsync(secondId));
            Assert.Equal(5000m, (await SavingsService.GetSnapshotAsync(check, CancellationToken.None)).Available);
            Assert.Null((await check.Buckets.SingleAsync()).GoalId);
        }
    }

    [Fact]
    public async Task Editing_A_Goal_Does_Not_Change_The_Money_In_It()
    {
        var (connection, options) = await CreateDatabaseAsync(savingsAvailable: 0m);
        await using (connection)
        {
            var service = new GoalService(new TestDbContextFactory(options));
            var goalId = await AddGoalAsync(options, target: 3000m, current: 1000m);

            await service.UpdateAsync(new Goal { Id = goalId, Name = "Bigger trip", TargetAmount = 800m, CurrentAmount = 0m });

            var (goal, _) = await LoadAsync(options, goalId);
            Assert.Equal("Bigger trip", goal.Name);
            Assert.Equal(1000m, goal.CurrentAmount);
            Assert.Equal(GoalStatus.Completed, goal.Status);
        }
    }

    private static async Task<int> AddGoalAsync(DbContextOptions<CoinStackDbContext> options, decimal target, decimal current = 0m)
    {
        await using var db = new CoinStackDbContext(options);
        var goal = new Goal { Name = "Trip", TargetAmount = target, CurrentAmount = current };
        db.Goals.Add(goal);
        await db.SaveChangesAsync();
        return goal.Id;
    }

    private static async Task<(Goal Goal, SavingsSnapshot Savings)> LoadAsync(DbContextOptions<CoinStackDbContext> options, int goalId)
    {
        await using var db = new CoinStackDbContext(options);
        return (await db.Goals.SingleAsync(g => g.Id == goalId), await SavingsService.GetSnapshotAsync(db, CancellationToken.None));
    }

    private static async Task<(SqliteConnection Connection, DbContextOptions<CoinStackDbContext> Options)> CreateDatabaseAsync(decimal savingsAvailable)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CoinStackDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new CoinStackDbContext(options);
        await db.Database.EnsureCreatedAsync();
        // Goal money never leaves the savings account; a goal only sets part of it aside.
        db.Accounts.Add(new Account { Name = "Savings", Type = AccountType.Savings, Balance = savingsAvailable, BalanceAsOfUtc = DateTime.UtcNow.AddDays(-1) });
        db.SavingsState.Add(new SavingsState());
        await db.SaveChangesAsync();
        return (connection, options);
    }
}
