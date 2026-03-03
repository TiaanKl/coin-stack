namespace CoinStack.Data.Entities;

public sealed class SavingsFallbackEvent : EntityBase
{
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public decimal AmountUsed { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;
}
