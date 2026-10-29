using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

public sealed class DataResetServiceTests
{
    [Fact]
    public async Task Reset_Clears_App_Data_Keeps_Accounts_And_Reseeds()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CoinStackDbContext>().UseSqlite(connection).Options;

        await using (var db = new CoinStackDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new ApplicationUser { UserName = "tiaan", Email = "tiaan@example.com" });
            var category = new Category { Name = "Custom", ColorHex = "#000000" };
            db.Categories.Add(category);
            db.Transactions.Add(new Transaction { Description = "Coffee", Amount = 35m, Category = category });
            db.AppSettings.Add(new AppSettings { Currency = "ZAR", MonthlyIncome = 25_000m });
            db.SavingsState.Add(new SavingsState { Total = 500m, Available = 500m });
            await db.SaveChangesAsync();
        }

        var factory = new TestDbContextFactory(options);
        var reset = new DataResetService(
            factory,
            new AchievementService(factory, new FakeScoringService()),
            new LevelService(factory));

        var raised = false;
        reset.DataResetCompleted += () => raised = true;

        var backup = await reset.ResetAllDataAsync();

        await using var verify = new CoinStackDbContext(options);
        Assert.Null(backup); // in-memory database: nothing to copy
        Assert.True(raised);
        Assert.Equal(1, await verify.Users.CountAsync());
        Assert.Empty(await verify.Transactions.ToListAsync());
        Assert.Empty(await verify.AppSettings.ToListAsync());
        Assert.Empty(await verify.SavingsState.ToListAsync());
        Assert.DoesNotContain(await verify.Categories.ToListAsync(), c => c.Name == "Custom");
        Assert.Contains(await verify.Categories.ToListAsync(), c => c.Name == "Groceries");
        Assert.NotEmpty(await verify.Achievements.ToListAsync());
        Assert.Equal(1, await verify.UserLevels.CountAsync());
    }
}
