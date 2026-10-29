using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services.Import;

/// <summary>
/// The categories the person chose for merchants (<see cref="MerchantRule"/>). They win over the defaults in
/// <see cref="MerchantNormalizer"/> on every import. A shop the app doesn't recognise is also matched by its
/// first word when every remembered shop starting with that word agrees: NANDOS ROSEBANK remembered as
/// Dining covers NANDOS MENLYN, while UBER EATS and UBER TRIP stay apart because "UBER" is too short to trust.
/// </summary>
public sealed class MerchantRuleBook
{
    private const int MinimumBrandLength = 5;
    private const int MaxNameLength = 200;

    private readonly Dictionary<string, string> _categoryByKey;

    private MerchantRuleBook(Dictionary<string, string> categoryByKey)
    {
        _categoryByKey = categoryByKey;
    }

    public static async Task<MerchantRuleBook> LoadAsync(CoinStackDbContext db, CancellationToken cancellationToken)
    {
        var rules = await db.MerchantRules
            .AsNoTracking()
            .Select(r => new { r.NormalizedKey, Category = r.Category!.Name })
            .ToListAsync(cancellationToken);

        return new MerchantRuleBook(rules.ToDictionary(r => r.NormalizedKey, r => r.Category, StringComparer.Ordinal));
    }

    /// <summary>The category the person chose for this merchant, or null to keep the default.</summary>
    public string? CategoryFor(string normalizedKey)
    {
        if (_categoryByKey.TryGetValue(normalizedKey, out var category))
        {
            return category;
        }

        if (MerchantNormalizer.IsKnownKey(normalizedKey) || BrandOf(normalizedKey) is not { } brand)
        {
            return null;
        }

        var sameBrand = _categoryByKey
            .Where(rule => !MerchantNormalizer.IsKnownKey(rule.Key) && BrandOf(rule.Key) == brand)
            .Select(rule => rule.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return sameBrand.Count == 1 ? sameBrand[0] : null;
    }

    /// <summary>The merchant key of a saved transaction. Bank rows keep the bank's own wording in Notes.</summary>
    public static string KeyOf(Transaction transaction)
        => MerchantNormalizer.Normalize(transaction.Notes ?? transaction.Description, transaction.Type).NormalizedKey;

    /// <summary>Remembers a merchant's category, replacing any earlier choice. The caller saves.</summary>
    public static async Task RememberAsync(
        CoinStackDbContext db,
        string normalizedKey,
        string merchantName,
        int categoryId,
        CancellationToken cancellationToken)
    {
        var rule = db.MerchantRules.Local.FirstOrDefault(r => r.NormalizedKey == normalizedKey)
                   ?? await db.MerchantRules.FirstOrDefaultAsync(r => r.NormalizedKey == normalizedKey, cancellationToken);
        if (rule is null)
        {
            rule = new MerchantRule { NormalizedKey = normalizedKey };
            db.MerchantRules.Add(rule);
        }

        rule.MerchantName = merchantName.Length > MaxNameLength ? merchantName[..MaxNameLength] : merchantName;
        rule.CategoryId = categoryId;
    }

    private static string? BrandOf(string key)
    {
        var first = key.Split('_', 2)[0];
        return first.Length >= MinimumBrandLength && first.All(char.IsLetter) ? first : null;
    }
}
