namespace FinanceManager.Services;

public interface IDataResetService
{
    Task ResetAllDataAsync(CancellationToken cancellationToken = default);
}