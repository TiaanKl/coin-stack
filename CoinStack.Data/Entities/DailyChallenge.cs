namespace CoinStack.Data.Entities;

public sealed class DailyChallenge : EntityBase
{
    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public string Icon { get; set; } = "fa-bolt";

    public int XpReward { get; set; }

    public ChallengeFrequency Frequency { get; set; }

    public ChallengeStatus Status { get; set; }

    public DateTime AssignedDateUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
}
