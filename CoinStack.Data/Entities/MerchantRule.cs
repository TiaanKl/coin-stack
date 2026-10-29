namespace CoinStack.Data.Entities;

/// <summary>
/// A category the person chose for a merchant, by changing it in an import preview or on one of the
/// merchant's bank transactions. Every later import puts that merchant in this category.
/// </summary>
public sealed class MerchantRule : EntityBase
{
    /// <summary>The merchant key from <c>MerchantNormalizer</c>, e.g. "CHECKERS" or "NANDOS_ROSEBANK".</summary>
    public string NormalizedKey { get; set; } = "";

    /// <summary>The merchant as the person last saw it, for showing the rule.</summary>
    public string MerchantName { get; set; } = "";

    public int CategoryId { get; set; }

    public Category? Category { get; set; }
}
