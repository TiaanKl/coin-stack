namespace CoinStack.Data.Entities;

public sealed class Subscription : EntityBase
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";

    public SubscriptionCycle Cycle { get; set; } = SubscriptionCycle.Monthly;

    public decimal Cost { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    /// <summary>
    /// Primary UI color for the subscription (e.g. #RRGGBB).
    /// </summary>
    public string? ColorHex { get; set; }

    /// <summary>
    /// Optional override used by the UI.
    /// </summary>
    public string? CustomHexColor { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
