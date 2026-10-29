using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface IReflectionService
{
    Task<Reflection> CreateReflectionAsync(ReflectionTrigger trigger, int? transactionId = null, CancellationToken cancellationToken = default);
    Task CompleteReflectionAsync(int reflectionId, string response, int moodBefore, int moodAfter, EmotionTag? emotionTag = null, CancellationToken cancellationToken = default);
    Task<List<Reflection>> GetRecentAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<Reflection?> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<List<EmotionSpendPattern>> GetEmotionPatternsAsync(CancellationToken cancellationToken = default);
}

public sealed record EmotionSpendPattern(EmotionTag Tag, int Count, decimal TotalSpend, string Insight);
