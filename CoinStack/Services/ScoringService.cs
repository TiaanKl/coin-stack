using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class ScoringService : IScoringService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    public ScoringService(IDbContextFactory<CoinStackDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<int> GetTotalScoreAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.ScoreEvents
            .AsNoTracking()
            .SumAsync(x => x.Points, cancellationToken);
    }

    public async Task<List<ScoreEvent>> GetRecentEventsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.ScoreEvents
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<ScoreEvent> AddScoreEventAsync(
        int points,
        ScoreChangeReason reason,
        string description,
        int? transactionId = null,
        int? bucketId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var evt = new ScoreEvent
        {
            Points = points,
            Reason = reason,
            Description = description,
            TransactionId = transactionId,
            BucketId = bucketId,
        };
        db.ScoreEvents.Add(evt);
        await db.SaveChangesAsync(cancellationToken);
        return evt;
    }

    public async Task EvaluateTransactionAsync(
        Transaction transaction,
        decimal bucketAllocated,
        decimal bucketSpentBefore,
        CancellationToken cancellationToken = default)
    {
        if (transaction.Type != TransactionType.Expense)
        {
            return;
        }

        if (transaction.ExpenseKind == ExpenseKind.ForceMajeure)
        {
            await AddScoreEventAsync(
                0,
                ScoreChangeReason.ForceMajeure,
                $"Force majeure expense — no score impact",
                transaction.Id,
                transaction.BucketId,
                cancellationToken);
            return;
        }

        var spentAfter = bucketSpentBefore + transaction.Amount;
        var wasUnder = bucketSpentBefore <= bucketAllocated;
        var isNowOver = spentAfter > bucketAllocated;

        if (wasUnder && !isNowOver)
        {
            await AddScoreEventAsync(
                5,
                ScoreChangeReason.UnderBudget,
                $"Stayed under budget in bucket spending",
                transaction.Id,
                transaction.BucketId,
                cancellationToken);
        }
        else if (isNowOver)
        {
            await AddScoreEventAsync(
                -10,
                ScoreChangeReason.OverBudget,
                $"Went over budget — spent {spentAfter:C} of {bucketAllocated:C} allocated",
                transaction.Id,
                transaction.BucketId,
                cancellationToken);
        }

        if (transaction.IsImpulse)
        {
            await AddScoreEventAsync(
                -5,
                ScoreChangeReason.ImpulseBuy,
                "Impulse purchase recorded",
                transaction.Id,
                transaction.BucketId,
                cancellationToken);
        }
    }
}
