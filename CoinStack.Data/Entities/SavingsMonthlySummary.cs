namespace CoinStack.Data.Entities;

public sealed class SavingsMonthlySummary : EntityBase
{
    public string Month { get; set; } = string.Empty;

    public decimal Base { get; set; } = 0;

    public decimal Interest { get; set; } = 0;

    public decimal Total { get; set; } = 0;

    public decimal RunningTotal { get; set; } = 0;
}
