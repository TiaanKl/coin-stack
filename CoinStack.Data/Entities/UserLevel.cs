namespace CoinStack.Data.Entities;

public sealed class UserLevel : EntityBase
{
    public int Level { get; set; } = 1;

    public int CurrentXp { get; set; }

    public int TotalXp { get; set; }

    public string Title { get; set; } = "Penny Starter";
}
