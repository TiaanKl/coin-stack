using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public static class DatabaseInitializer
{
    private static readonly Category[] DefaultCategories =
    [
        new() { Name = "Groceries", ColorHex = "#22C55E", Scope = CategoryScope.Expense },
        new() { Name = "Housing", ColorHex = "#6366F1", Scope = CategoryScope.Expense },
        new() { Name = "Utilities", ColorHex = "#0EA5E9", Scope = CategoryScope.Expense },
        new() { Name = "Transport", ColorHex = "#F97316", Scope = CategoryScope.Expense },
        new() { Name = "Health", ColorHex = "#EF4444", Scope = CategoryScope.Expense },
        new() { Name = "Dining", ColorHex = "#F59E0B", Scope = CategoryScope.Expense },
        new() { Name = "Entertainment", ColorHex = "#A855F7", Scope = CategoryScope.Expense },
        new() { Name = "Shopping", ColorHex = "#EC4899", Scope = CategoryScope.Expense },
        new() { Name = "Salary", ColorHex = "#10B981", Scope = CategoryScope.Income },
        new() { Name = "Freelance", ColorHex = "#14B8A6", Scope = CategoryScope.Income },
        new() { Name = "Savings", ColorHex = "#3B82F6", Scope = CategoryScope.Both }
    ];

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoinStackDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await SeedMissingDefaultCategoriesAsync(db, cancellationToken);

        var achievementService = scope.ServiceProvider.GetRequiredService<IAchievementService>();
        await achievementService.SeedAchievementsAsync(cancellationToken);

        var levelService = scope.ServiceProvider.GetRequiredService<ILevelService>();
        await levelService.GetCurrentLevelAsync(cancellationToken); // ensures UserLevel row exists
    }

    public static async Task SeedMissingDefaultCategoriesAsync(CoinStackDbContext db, CancellationToken cancellationToken)
    {
        var existingNames = await db.Categories
            .AsNoTracking()
            .Select(c => c.Name)
            .ToListAsync(cancellationToken);

        var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = DefaultCategories
            .Where(c => !existing.Contains(c.Name))
            .Select(c => new Category
            {
                Name = c.Name,
                ColorHex = c.ColorHex,
                Scope = c.Scope
            })
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        db.Categories.AddRange(missing);

        await db.SaveChangesAsync(cancellationToken);
    }
}
