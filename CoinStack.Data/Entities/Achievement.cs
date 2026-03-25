namespace CoinStack.Data.Entities;

public sealed class Achievement : EntityBase
{
    public string Key { get; set; } = "";

    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public string Icon { get; set; } = "fa-trophy";

    public AchievementCategory Category { get; set; }

    public int XpReward { get; set; }

    public bool IsUnlocked { get; set; }

    public DateTime? UnlockedAtUtc { get; set; }
}
