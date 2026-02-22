namespace CoinStack.Data.Entities;

public sealed class WaitlistItem : EntityBase
{
    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public decimal EstimatedCost { get; set; }

    public string? Url { get; set; }

    public WaitlistPriority Priority { get; set; } = WaitlistPriority.Medium;

    public CoolOffPeriod CoolOffPeriod { get; set; } = CoolOffPeriod.Days7;

    public DateTime CoolOffUntil { get; set; }

    public bool IsUnlocked { get; set; }

    public EmotionTag? EmotionAtTimeOfAdding { get; set; }

    public int Score { get; set; }

    public DateTime LastEvaluated { get; set; } = DateTime.UtcNow;

    public bool IsPurchased { get; set; }

    public DateTime? PurchasedAtUtc { get; set; }

    public string? ReflectionNote { get; set; }
}
