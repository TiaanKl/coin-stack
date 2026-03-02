namespace CoinStack.Data.Entities;

/// <summary>
/// Records each instance where savings were used as a fallback buffer.
/// </summary>
public sealed class SavingsFallbackEvent : EntityBase
{
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public decimal AmountUsed { get; set; }

    /// <summary>Human-readable reason: "Bucket empty", "Subscription", "Debt payment", etc.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Name of the bucket / subscription / debt that triggered the fallback.</summary>
    public string SourceName { get; set; } = string.Empty;
}
