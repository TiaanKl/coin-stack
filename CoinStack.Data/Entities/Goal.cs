namespace CoinStack.Data.Entities;

public sealed class Goal : EntityBase
{
    public string Name { get; set; } = "";

    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }

    public DateTime? TargetDateUtc { get; set; }

    public GoalStatus Status { get; set; } = GoalStatus.Active;
}
