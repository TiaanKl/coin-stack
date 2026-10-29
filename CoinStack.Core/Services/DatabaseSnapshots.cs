using CoinStack.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

/// <summary>Safety copies of the database, written next to it under <c>backups/</c> before anything replaces its contents.</summary>
public static class DatabaseSnapshots
{
    /// <summary>
    /// Writes a consistent copy of the SQLite file, named after why it was made ("reset", "restore").
    /// Returns the path, or null for in-memory or non-SQLite databases.
    /// </summary>
    public static async Task<string?> SaveCopyAsync(CoinStackDbContext db, string reason, CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlite() || db.Database.GetDbConnection() is not SqliteConnection connection || IsInMemory(connection))
        {
            return null;
        }

        var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(connection.DataSource)) ?? ".", "backups");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"coinstack-before-{reason}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db");

        // VACUUM INTO takes any string expression, so the path goes in as a bound parameter.
        await db.Database.ExecuteSqlAsync($"VACUUM INTO {path}", cancellationToken);
        return path;
    }

    private static bool IsInMemory(SqliteConnection connection)
        => string.IsNullOrWhiteSpace(connection.DataSource)
           || connection.DataSource.Contains(":memory:", StringComparison.OrdinalIgnoreCase)
           || new SqliteConnectionStringBuilder(connection.ConnectionString).Mode == SqliteOpenMode.Memory;
}
