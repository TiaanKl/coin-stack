using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface ISettingsService
{
    event Action? SettingsChanged;

    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
