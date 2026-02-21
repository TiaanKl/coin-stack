using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface IScoringService
{
    Task<int> GetTotalScoreAsync(CancellationToken cancellationToken = default);
    Task<List<ScoreEvent>> GetRecentEventsAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<ScoreEvent> AddScoreEventAsync(int points, ScoreChangeReason reason, string description, int? transactionId = null, int? bucketId = null, CancellationToken cancellationToken = default);
    Task EvaluateTransactionAsync(Transaction transaction, decimal bucketAllocated, decimal bucketSpentBefore, CancellationToken cancellationToken = default);
}
