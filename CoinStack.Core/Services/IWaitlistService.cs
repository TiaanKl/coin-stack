using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface IWaitlistService
{
    Task<List<WaitlistItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<WaitlistItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<WaitlistItem> CreateAsync(WaitlistItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(WaitlistItem item, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the item purchased and creates a real expense transaction (impulse) so budgets/reports stay accurate.
    /// </summary>
    Task<(WaitlistItem? Item, GameTransactionResult? Result)> MarkPurchasedAsync(
        int id,
        int? bucketId = null,
        int? categoryId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly records that the user decided not to buy after cool-off. Awards ImpulseResisted points
    /// and increments the NoImpulseBuy streak. Distinct from <see cref="DeleteAsync"/> so accidental deletes are not rewarded.
    /// </summary>
    Task MarkResistedAsync(int id, CancellationToken cancellationToken = default);

    Task EvaluateCoolOffsAsync(CancellationToken cancellationToken = default);
    Task<int> CalculateReadinessScoreAsync(int itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a short cost-framing sentence for the given amount (e.g. days of savings contributions).
    /// </summary>
    Task<string?> GetCostFramingMessageAsync(decimal estimatedCost, CancellationToken cancellationToken = default);

    /// <summary>Current NoImpulseBuy streak count (0 if none).</summary>
    Task<int> GetNoImpulseBuyStreakAsync(CancellationToken cancellationToken = default);

    /// <summary>Resets the NoImpulseBuy streak after an impulse purchase.</summary>
    Task ResetNoImpulseBuyStreakAsync(CancellationToken cancellationToken = default);
}
