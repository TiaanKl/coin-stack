using CoinStack.Data;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

/// <summary>
/// Clears every CoinStack record.
/// </summary>
/// <remarks>
/// This used to call <c>EnsureDeletedAsync</c>, which dropped the whole SQLite file with no way back,
/// and only re-seeded categories (achievements stayed empty until restart).
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

        var backupPath = await DatabaseSnapshots.SaveCopyAsync(db, "reset", cancellationToken);

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
            await db.MerchantRules.ExecuteDeleteAsync(cancellationToken);
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
            await db.DismissedRecurringMerchants.ExecuteDeleteAsync(cancellationToken);

            if (db.Database.IsSqlite())
            {
                // Restart ids at 1, as the old drop-and-recreate did.
                await db.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence", cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        await DatabaseInitializer.SeedMissingDefaultCategoriesAsync(db, cancellationToken);
        await _achievementService.SeedAchievementsAsync(cancellationToken);
        await _levelService.GetCurrentLevelAsync(cancellationToken); // recreates the UserLevel row

        DataResetCompleted?.Invoke();
        return backupPath;
    }
}
