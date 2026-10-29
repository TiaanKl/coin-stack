using CoinStack.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

/// <summary>
/// Encrypted backup and restore: one <c>.coinstack</c> file holding everything, locked with a password the
/// person chooses. Each app saves it wherever the person likes (iCloud Drive, Google Drive, OneDrive, a
/// folder) and any of the apps can restore it, so it also moves data to a new phone or computer.
/// </summary>
public interface IBackupService
{
    /// <summary>A name for a new backup file, with today's date.</summary>
    string SuggestedFileName { get; }

    /// <summary>Everything, compressed and encrypted. The password needs at least eight characters.</summary>
    Task<byte[]> CreateAsync(string password, CancellationToken cancellationToken = default);

    /// <summary>What a backup holds, so the person can check it before restoring. Throws <see cref="BackupException"/>.</summary>
    Task<BackupManifest> ReadAsync(byte[] backup, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces all data with the backup's. A copy of the current data is saved first; its path is returned
    /// (null for an in-memory database). Throws <see cref="BackupException"/> and leaves everything as it was
    /// for a wrong password, a damaged file, or a backup from a newer version of the app.
    /// </summary>
    Task<string?> RestoreAsync(byte[] backup, string password, CancellationToken cancellationToken = default);
}

public sealed class BackupService : IBackupService
{
    /// <summary>Far beyond any real CoinStack database; a larger file isn't a backup of one.</summary>
    public const int MaxBackupBytes = 256 * 1024 * 1024;

    private const int ManifestFormat = 1;

    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;
    private readonly TimeProvider _clock;
    private readonly int _iterations;

    public BackupService(IDbContextFactory<CoinStackDbContext> dbFactory, TimeProvider? clock = null)
        : this(dbFactory, clock, BackupFile.DefaultIterations)
    {
    }

    /// <summary>Tests use fewer PBKDF2 rounds; real backups always use the default.</summary>
    internal BackupService(IDbContextFactory<CoinStackDbContext> dbFactory, TimeProvider? clock, int iterations)
    {
        _dbFactory = dbFactory;
        _clock = clock ?? TimeProvider.System;
        _iterations = iterations;
    }

    public string SuggestedFileName => $"coinstack-backup-{_clock.Today():yyyy-MM-dd}.coinstack";

    public async Task<byte[]> CreateAsync(string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(password) || password.Length < BackupFile.MinimumPasswordLength)
        {
            throw new BackupException($"Use a password of at least {BackupFile.MinimumPasswordLength} characters.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var dates = await db.Transactions.Select(t => t.OccurredAtUtc).ToListAsync(cancellationToken);
        var manifest = new BackupManifest(
            ManifestFormat,
            _clock.GetUtcNow().UtcDateTime,
            (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).LastOrDefault() ?? "",
            dates.Count,
            dates.Count > 0 ? dates.Min() : null,
            dates.Count > 0 ? dates.Max() : null);

        var database = await WithTempFileAsync(async path =>
        {
            // SQLite's backup API gives a consistent copy even while the app is using the database.
            await using (var copy = OpenFile(path))
            {
                await copy.OpenAsync(cancellationToken);
                (await OpenLiveAsync(db, cancellationToken)).BackupDatabase(copy);
            }

            return await File.ReadAllBytesAsync(path, cancellationToken);
        });

        return BackupFile.Write(manifest, database, password, _iterations);
    }

    public async Task<BackupManifest> ReadAsync(byte[] backup, string password, CancellationToken cancellationToken = default)
    {
        var (manifest, _) = await OpenAsync(backup, password, cancellationToken);
        return manifest;
    }

    public async Task<string?> RestoreAsync(byte[] backup, string password, CancellationToken cancellationToken = default)
    {
        var (_, database) = await OpenAsync(backup, password, cancellationToken);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var safetyCopy = await DatabaseSnapshots.SaveCopyAsync(db, "restore", cancellationToken);

        await WithTempFileAsync(async path =>
        {
            await File.WriteAllBytesAsync(path, database, cancellationToken);
            await using var source = OpenFile(path);
            await source.OpenAsync(cancellationToken);

            if (!await LooksLikeCoinStackAsync(source, cancellationToken))
            {
                throw new BackupException("This backup file is damaged.");
            }

            // Copies the backup over the live database in place, so every open connection sees the restored data.
            source.BackupDatabase(await OpenLiveAsync(db, cancellationToken));
            return true;
        });

        // An older backup is brought up to this version of the app. (A database made without migrations, as in
        // tests, has no history to continue from.)
        if ((await db.Database.GetAppliedMigrationsAsync(cancellationToken)).Any())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        await DatabaseInitializer.SeedMissingDefaultCategoriesAsync(db, cancellationToken);
        return safetyCopy;
    }

    private async Task<(BackupManifest Manifest, byte[] Database)> OpenAsync(byte[] backup, string password, CancellationToken cancellationToken)
    {
        if (backup.Length > MaxBackupBytes)
        {
            throw new BackupException("This file is too large to be a CoinStack backup.");
        }

        var (manifest, database) = BackupFile.Read(backup, password ?? "");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        if (manifest.SchemaVersion.Length > 0 && !db.Database.GetMigrations().Contains(manifest.SchemaVersion))
        {
            throw new BackupException("This backup was made by a newer version of CoinStack. Update the app, then restore it.");
        }

        return (manifest, database);
    }

    private static async Task<SqliteConnection> OpenLiveAsync(CoinStackDbContext db, CancellationToken cancellationToken)
    {
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return connection;
    }

    private static async Task<bool> LooksLikeCoinStackAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Transactions', 'AppSettings', 'Categories')";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 3;
    }

    /// <summary>Pooling off, so the file is released and can be deleted as soon as the connection closes.</summary>
    private static SqliteConnection OpenFile(string path)
        => new(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());

    private static async Task<T> WithTempFileAsync<T>(Func<string, Task<T>> work)
    {
        var path = Path.Combine(Path.GetTempPath(), $"coinstack-{Guid.NewGuid():N}.db");
        try
        {
            return await work(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
