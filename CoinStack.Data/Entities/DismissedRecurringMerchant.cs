namespace CoinStack.Data.Entities;

/// <summary>
/// A merchant whose suggested subscription or bucket the user deleted, so a later bank scan or import
/// does not create it again.
/// </summary>
public sealed class DismissedRecurringMerchant : EntityBase
{
    /// <summary>The merchant key from <c>MerchantNormalizer</c>, e.g. "NETFLIX".</summary>
    public string NormalizedKey { get; set; } = "";

    public bool SubscriptionDismissed { get; set; }

    public bool BucketDismissed { get; set; }
}
