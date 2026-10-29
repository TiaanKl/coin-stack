using CoinStack.Data;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

/// <summary>
/// Clears every CoinStack record while keeping sign-in accounts (the ASP.NET Identity tables).
/// </summary>
/// <remarks>
/// This used to call <c>EnsureDeletedAsync</c>, which dropped the whole SQLite file — Identity users
/// included — with no way back, and only re-seeded categories (achievements stayed empty until restart).
/// Now a copy of the database is written first, the app tables are emptied in one transaction, and the
/// same seeding the app runs at startup is repeated.
/// </remarks>
public sealed class DataResetService : IDataResetService
{
    public event Action? DataResetCompleted;

    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;
    private readonly IAchievementService _achievementService;
    private readonly ILevelService _levelService;

    public DataResetService(
        IDbContextFactory<CoinStackDbContext> dbFactory,
        IAchievementService achievementService,
        ILevelService levelService)
    {
        _dbFactory = dbFactory;
        _achievementService = achievementService;
        _levelService = levelService;
    }

    public async Task<string?> ResetAllDataAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var backupPath = await BackupAsync(db, cancellationToken);

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            // Children before parents so no foreign key blocks a delete.
            await db.ScoreEvents.ExecuteDeleteAsync(cancellationToken);
            await db.Reflections.ExecuteDeleteAsync(cancellationToken);
            await db.SavingsFallbackEvents.ExecuteDeleteAsync(cancellationToken);
            await db.Transactions.ExecuteDeleteAsync(cancellationToken);
            await db.Budgets.ExecuteDeleteAsync(cancellationToken);
            await db.WaitlistItems.ExecuteDeleteAsync(cancellationToken);
            await db.Buckets.ExecuteDeleteAsync(cancellationToken);
            await db.Subscriptions.ExecuteDeleteAsync(cancellationToken);
            await db.Goals.ExecuteDeleteAsync(cancellationToken);
            await db.DebtAccounts.ExecuteDeleteAsync(cancellationToken);
            await db.StatementImports.ExecuteDeleteAsync(cancellationToken);
            await db.Categories.ExecuteDeleteAsync(cancellationToken);
            await db.Streaks.ExecuteDeleteAsync(cancellationToken);
            await db.AppSettings.ExecuteDeleteAsync(cancellationToken);
            await db.SavingsState.ExecuteDeleteAsync(cancellationToken);
            await db.SavingsMonthlySummaries.ExecuteDeleteAsync(cancellationToken);
            await db.CbtJournalEntries.ExecuteDeleteAsync(cancellationToken);
            await db.Achievements.ExecuteDeleteAsync(cancellationToken);
            await db.UserLevels.ExecuteDeleteAsync(cancellationToken);
            await db.DailyChallenges.ExecuteDeleteAsync(cancellationToken);
            await db.WeeklyRecaps.ExecuteDeleteAsync(cancellationToken);

            if (db.Database.IsSqlite())
            {
                // Restart ids at 1 for the app tables, as the old drop-and-recreate did; Identity keeps its own.
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM sqlite_sequence WHERE name NOT LIKE 'AspNet%'", cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        await DatabaseInitializer.SeedMissingDefaultCategoriesAsync(db, cancellationToken);
        await _achievementService.SeedAchievementsAsync(cancellationToken);
        await _levelService.GetCurrentLevelAsync(cancellationToken); // recreates the UserLevel row

        DataResetCompleted?.Invoke();
        return backupPath;
    }

    /// <summary>
    /// Writes a consistent copy of the SQLite file next to it, under <c>backups/</c>.
    /// Returns the path, or null for in-memory or non-SQLite databases.
    /// </summary>
    private static async Task<string?> BackupAsync(CoinStackDbContext db, CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlite())
        {
            return null;
        }

        var dataSource = db.Database.GetDbConnection().DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dataSource)) ?? ".", "backups");
        Directory.CreateDirectory(directory);

        var backupPath = Path.Combine(directory, $"coinstack-before-reset-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db");

        // VACUUM INTO takes any string expression, so the path goes in as a bound parameter.
        await db.Database.ExecuteSqlAsync($"VACUUM INTO {backupPath}", cancellationToken);
        return backupPath;
    }
}
