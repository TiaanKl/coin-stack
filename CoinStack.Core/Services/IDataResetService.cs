namespace CoinStack.Services;

public interface IDataResetService
{
    event Action? DataResetCompleted;

    /// <summary>
    /// Backs up the database, then clears all CoinStack data and re-seeds defaults.
    /// Returns the backup file path, or null when no file backup was possible.
    /// </summary>
    Task<string?> ResetAllDataAsync(CancellationToken cancellationToken = default);
}
