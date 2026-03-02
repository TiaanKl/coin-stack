namespace CoinStack.Data.Entities;

/// <summary>
/// Singleton savings state row — always stored with Id = 1.
/// </summary>
public sealed class SavingsState : EntityBase
{
    /// <summary>Cumulative total ever saved (base + interest), never decreases except resets.</summary>
    public decimal Total { get; set; } = 0;

    /// <summary>Amount available to spend / use as fallback (Total minus anything consumed).</summary>
    public decimal Available { get; set; } = 0;

    /// <summary>Amount currently reserved / allocated but not yet consumed.</summary>
    public decimal Reserved { get; set; } = 0;

    /// <summary>Whether savings can be dipped into to cover overspending.</summary>
    public bool FallbackEnabled { get; set; } = false;

    /// <summary>Last month for which the monthly calculation was applied (e.g. "2026-02").</summary>
    public string? LastCalculatedMonth { get; set; }
}
