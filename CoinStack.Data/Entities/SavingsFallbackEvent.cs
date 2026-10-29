namespace CoinStack.Data.Entities;

public sealed class SavingsFallbackEvent : EntityBase
{
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public decimal AmountUsed { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    /// <summary>True when the draw came out of the emergency fund rather than savings.</summary>
    public bool FromEmergencyFund { get; set; }

    /// <summary>The transaction that caused the draw, so it can be refunded on edit or delete.</summary>
    public int? TransactionId { get; set; }
}
