using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoinStack.Mobile.Services;

public interface IMobileDatabaseInitializationService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class MobileDatabaseInitializationService : IMobileDatabaseInitializationService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;
    private readonly ILogger<MobileDatabaseInitializationService> _logger;

    public MobileDatabaseInitializationService(
        IDbContextFactory<CoinStackDbContext> dbFactory,
        ILogger<MobileDatabaseInitializationService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        await db.Database.MigrateAsync(cancellationToken);
        await SeedDefaultsAsync(db, cancellationToken);
    }

    private async Task SeedDefaultsAsync(CoinStackDbContext db, CancellationToken cancellationToken)
    {
        var changed = false;

        var existingSettings = await db.AppSettings.FirstOrDefaultAsync(cancellationToken);
        if (existingSettings is null)
        {
            db.AppSettings.Add(new AppSettings
            {
                Currency = "USD",
                MonthStartDay = 1,
                MonthlyIncome = 5000m,
                EnableReflections = true,
                EnableScoring = true,
                EnableStreaks = true,
                EnableToast = true,
                LargeExpenseThreshold = 50,
            });

            changed = true;
        }

        if (!await db.Buckets.AnyAsync(cancellationToken))
        {
            if (!await db.Categories.AnyAsync(cancellationToken))
            {
                db.Categories.AddRange(
                    new Category { Name = "Groceries", ColorHex = "#22C55E", Scope = CategoryScope.Expense },
                    new Category { Name = "Entertainment", ColorHex = "#A855F7", Scope = CategoryScope.Expense },
                    new Category { Name = "Savings", ColorHex = "#3B82F6", Scope = CategoryScope.Both }
                );

                await db.SaveChangesAsync(cancellationToken);
                changed = true;
            }

            var categoriesByName = await db.Categories
                .AsNoTracking()
                .ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var defaults = new[]
            {
                new Bucket
                {
                    Name = "Essentials",
                    AllocatedAmount = 1200m,
                    IsDefault = true,
                    SortOrder = 1,
                    IsSavings = false,
                    CategoryId = categoriesByName.GetValueOrDefault("Groceries")
                },
                new Bucket
                {
                    Name = "Lifestyle",
                    AllocatedAmount = 800m,
                    IsDefault = true,
                    SortOrder = 2,
                    IsSavings = false,
                    CategoryId = categoriesByName.GetValueOrDefault("Entertainment")
                },
                new Bucket
                {
                    Name = "Savings",
                    AllocatedAmount = 500m,
                    IsDefault = true,
                    SortOrder = 3,
                    IsSavings = true,
                    CategoryId = categoriesByName.GetValueOrDefault("Savings")
                }
            };

            db.Buckets.AddRange(defaults);
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Mobile SQLite database initialized with default seed data.");
        }
    }
}
