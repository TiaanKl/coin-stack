namespace CoinStack.Services;

public interface IDataResetService
{
    event Action? DataResetCompleted;

    Task ResetAllDataAsync(CancellationToken cancellationToken = default);
}