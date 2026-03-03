namespace CoinStack.Data.Entities;

public sealed class SavingsState : EntityBase
{
    public decimal Total { get; set; } = 0;

    public decimal Available { get; set; } = 0;

    public decimal Reserved { get; set; } = 0;

    public bool FallbackEnabled { get; set; } = false;

    public string? LastCalculatedMonth { get; set; }
}
