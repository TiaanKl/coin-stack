namespace CoinStack.Data.Entities;

/// <summary>
/// One row per calendar month of savings accumulation.
/// </summary>
public sealed class SavingsMonthlySummary : EntityBase
{
    /// <summary>Month key, e.g. "2026-02".</summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>Base contribution for this month (from fixed or % rule).</summary>
    public decimal Base { get; set; } = 0;

    /// <summary>Interest earned this month.</summary>
    public decimal Interest { get; set; } = 0;

    /// <summary>Base + Interest.</summary>
    public decimal Total { get; set; } = 0;

    /// <summary>Running cumulative total after this month.</summary>
    public decimal RunningTotal { get; set; } = 0;
}
