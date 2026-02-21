namespace CoinStack.Data.Entities;

public sealed class Streak : EntityBase
{
    public StreakType Type { get; set; }

    public int CurrentCount { get; set; }

    public int BestCount { get; set; }

    public DateTime LastIncrementedAtUtc { get; set; } = DateTime.UtcNow;
}
