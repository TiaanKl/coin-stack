namespace CoinStack.Data.Entities;

/// <summary>
/// What savings keeps besides money: the money in savings and the emergency fund lives in their accounts.
/// </summary>
public sealed class SavingsState : EntityBase
{
    /// <summary>Money in the savings account set aside for goals. It stays in savings but isn't free to use.</summary>
    public decimal Reserved { get; set; } = 0;

    public bool FallbackEnabled { get; set; } = false;

    public string? LastCalculatedMonth { get; set; }
}
