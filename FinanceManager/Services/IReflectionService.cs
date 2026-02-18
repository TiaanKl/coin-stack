using FinanceManager.Data.Entities;

namespace FinanceManager.Services;

public interface IReflectionService
{
    Task<Reflection> CreateReflectionAsync(ReflectionTrigger trigger, int? transactionId = null, CancellationToken cancellationToken = default);
    Task CompleteReflectionAsync(int reflectionId, string response, int moodBefore, int moodAfter, CancellationToken cancellationToken = default);
    Task<List<Reflection>> GetRecentAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<Reflection?> GetPendingAsync(CancellationToken cancellationToken = default);
}
