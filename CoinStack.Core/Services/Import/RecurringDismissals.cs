using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services.Import;

/// <summary>
/// Remembers merchants whose subscription or bucket the user deleted, so the recurring-payment scan
/// does not put them back.
/// </summary>
internal static class RecurringDismissals
{
    /// <summary>
    /// The merchant keys a subscription or bucket stands for: its own name, plus the merchants of the
    /// transactions linked to it. The caller saves.
    /// </summary>
    public static async Task RecordAsync(
        CoinStackDbContext db,
        string name,
        IEnumerable<string> linkedDescriptions,
        bool subscription,
        bool bucket,
        CancellationToken cancellationToken)
    {
        var keys = linkedDescriptions
            .Append(name)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => MerchantNormalizer.Normalize(d, TransactionType.Expense).NormalizedKey)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keys.Count == 0)
        {
            return;
        }

        var existing = await db.DismissedRecurringMerchants
            .Where(d => keys.Contains(d.NormalizedKey))
            .ToListAsync(cancellationToken);

        foreach (var key in keys)
        {
            var row = existing.FirstOrDefault(d => string.Equals(d.NormalizedKey, key, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                row = new DismissedRecurringMerchant { NormalizedKey = key };
                db.DismissedRecurringMerchants.Add(row);
            }

            row.SubscriptionDismissed |= subscription;
            row.BucketDismissed |= bucket;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
