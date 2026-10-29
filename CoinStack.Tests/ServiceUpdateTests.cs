using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

public sealed class ServiceUpdateTests
{
    [Fact]
    public async Task CategoryService_Update_Persists_Scope()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CoinStackDbContext>().UseSqlite(connection).Options;

        await using (var db = new CoinStackDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var service = new CategoryService(new TestDbContextFactory(options));
        var category = await service.CreateAsync(new Category { Name = "Side hustle", ColorHex = "#10B981", Scope = CategoryScope.Expense });

        category.Scope = CategoryScope.Income;
        await service.UpdateAsync(category);

        await using var verify = new CoinStackDbContext(options);
        Assert.Equal(CategoryScope.Income, (await verify.Categories.SingleAsync(c => c.Id == category.Id)).Scope);
    }

    [Fact]
    public async Task LevelService_Negative_Xp_Steps_Back_Down_A_Level()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CoinStackDbContext>().UseSqlite(connection).Options;

        await using (var db = new CoinStackDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var service = new LevelService(new TestDbContextFactory(options));

        var up = await service.AddXpAsync(60); // level 1 needs 50
        Assert.Equal(2, up.Level);
        Assert.Equal(10, up.CurrentXp);

        var down = await service.AddXpAsync(-15);
        Assert.Equal(1, down.Level);
        Assert.Equal(45, down.CurrentXp);
        Assert.Equal(45, down.TotalXp);

        var floor = await service.AddXpAsync(-500);
        Assert.Equal(1, floor.Level);
        Assert.Equal(0, floor.CurrentXp);
        Assert.Equal(0, floor.TotalXp);
    }
}
