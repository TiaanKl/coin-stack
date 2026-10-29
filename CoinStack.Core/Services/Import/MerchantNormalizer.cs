using System.Text.RegularExpressions;
using CoinStack.Data.Entities;

namespace CoinStack.Services.Import;

public sealed record MerchantInfo(
    string CanonicalName,
    string NormalizedKey,
    string CategoryName,
    string IconClass,
    bool IsAvoidableFee = false,
    string? FeeType = null);

/// <summary>
/// Turns a bank's transaction description into a merchant: a readable name, a key that groups its
/// variants ("Onlyfans*J", "Of" and "Onlyfans.Com*" are all OnlyFans), a category and an icon.
/// Merchants are matched on whole words, and when several match the longest phrase wins, so
/// "City Of Cape Town" isn't OnlyFans, "MR DIY" isn't Mr D and "Auto Spares" isn't Spar.
/// The person's own choices (<see cref="MerchantCategoryMemory"/>) take priority over the categories here.
/// </summary>
public static partial class MerchantNormalizer
{
    [GeneratedRegex(@"(?:POS\s+Purchase\s+)?(?:\d+(?:\.\d+)?\s+)?(?<desc>.*?)(?:\s+\d{6}\*\d{4}.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex PosPurchasePattern();

    [GeneratedRegex(@"\b\d{10,}\b")]
    private static partial Regex LongNumericReferenceRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleWhitespaceRegex();

    /// <summary>A merchant this app recognises. A phrase starting with "=" must be the whole description.</summary>
    private sealed record KnownMerchant(string Name, string Key, string Category, string Icon, params string[] Phrases)
    {
        /// <summary>Only matches money in (salary, refunds) or only money out; null matches both.</summary>
        public TransactionType? Only { get; init; }

        public bool IsAvoidableFee { get; init; }

        public string? FeeType { get; init; }
    }

    private static readonly KnownMerchant[] Known =
    [
        // Bank fees (FNB's wording)
        new("FNB Unpaid Item Fee", "FNB_UNPAID_FEE", "Banking Fees", "fa-solid fa-triangle-exclamation", "ITEM PAID NO FUNDS")
            { IsAvoidableFee = true, FeeType = "Unpaid Item (Insufficient Funds)" },
        new("FNB Declined Transaction Fee", "FNB_DECLINED_FEE", "Banking Fees", "fa-solid fa-circle-xmark",
            "DEBIT CARD POS UNSUCCESSFUL", "DECLINED PURCH", "DECLINED FOREIGN")
            { IsAvoidableFee = true, FeeType = "Declined Card Fee" },
        new("FNB Service & Foreign Fees", "FNB_SERVICE_FEES", "Banking Fees", "fa-solid fa-building-columns",
            "SERVICE FEES", "PAYMENTS BUNDLE FEE"),
        new("FNB Monthly Account Fee", "FNB_ACCOUNT_FEE", "Banking Fees", "fa-solid fa-building-columns",
            "MONTHLY ACCOUNT FEE", "MONTHLYACCOUNTFEE"),

        // Money in
        new("Salary", "SALARY", "Salary", "fa-solid fa-money-bill-wave", "SALARY", "SALARIS", "WAGES") { Only = TransactionType.Income },
        new("Refund / Fee Reversal", "REFUND", TransactionConventions.RefundsCategoryName, "fa-solid fa-rotate-left", "REFUND", "REVERSAL")
            { Only = TransactionType.Income },

        // Subscriptions and online services
        new("OnlyFans", "ONLYFANS", "Entertainment", "fa-solid fa-heart", "ONLYFANS", "=OF"),
        new("Steam", "STEAM", "Entertainment", "fa-brands fa-steam", "STEAM", "STEAMGAMES", "STEAMPOWERED"),
        new("Google Play & Services", "GOOGLE", "Entertainment", "fa-brands fa-google", "GOOGLE"),
        new("YouTube", "YOUTUBE", "Entertainment", "fa-brands fa-youtube", "YOUTUBE"),
        new("Microsoft", "MICROSOFT", "Entertainment", "fa-brands fa-microsoft", "MICROSOFT", "XBOX"),
        new("Netflix", "NETFLIX", "Entertainment", "fa-solid fa-film", "NETFLIX"),
        new("Spotify", "SPOTIFY", "Entertainment", "fa-brands fa-spotify", "SPOTIFY"),
        new("DStv", "DSTV", "Entertainment", "fa-solid fa-tv", "DSTV", "MULTICHOICE"),
        new("Showmax", "SHOWMAX", "Entertainment", "fa-solid fa-tv", "SHOWMAX"),
        new("Disney+", "DISNEY_PLUS", "Entertainment", "fa-solid fa-film", "DISNEY PLUS", "DISNEYPLUS"),
        new("Apple", "APPLE", "Entertainment", "fa-brands fa-apple", "APPLE.COM", "ITUNES"),
        new("Gumroad", "GUMROAD", "Entertainment", "fa-solid fa-bag-shopping", "GUMROAD"),
        new("Patreon", "PATREON", "Entertainment", "fa-brands fa-patreon", "PATREON"),
        new("ComfyOrg", "COMFYORG", "Entertainment", "fa-solid fa-palette", "COMFY.ORG", "COMFYORG"),
        new("WeMod", "WEMOD", "Entertainment", "fa-solid fa-gamepad", "WEMOD"),
        new("PayPal", "PAYPAL", "Entertainment", "fa-brands fa-paypal", "PAYPAL"),
        new("Gaming & In-App Purchases", "GAMING_CREDITS", "Entertainment", "fa-solid fa-dice", "GAME CREDITS", "COMEWELCOM", "TOTEM CORE"),
        new("IONOS Hosting", "IONOS", "Utilities", "fa-solid fa-server", "IONOS"),
        new("RunPod GPU", "RUNPOD", "Utilities", "fa-solid fa-microchip", "RUNPOD"),
        new("Cursor AI", "CURSOR", "Utilities", "fa-solid fa-code", "CURSOR"),
        new("OpenAI", "OPENAI", "Utilities", "fa-solid fa-robot", "OPENAI", "CHATGPT"),
        new("OpenRouter", "OPENROUTER", "Utilities", "fa-solid fa-network-wired", "OPENROUTER"),
        new("TensorDock", "TENSORDOCK", "Utilities", "fa-solid fa-server", "TENSORDOCK"),
        new("GitHub", "GITHUB", "Utilities", "fa-brands fa-github", "GITHUB"),

        // Groceries
        new("Pick n Pay", "PICK_N_PAY", "Groceries", "fa-solid fa-basket-shopping", "PICK N PAY", "PICKNPAY", "PNP"),
        new("Spar", "SPAR", "Groceries", "fa-solid fa-basket-shopping", "SPAR", "SUPERSPAR", "KWIKSPAR"),
        new("Groceries & Convenience", "GROCERIES_OTHER", "Groceries", "fa-solid fa-cart-shopping",
            "OK MINI MARK", "OK MINIMARK", "OK FOODS", "CHECKERS", "SHOPRITE", "WOOLWORTHS", "WOOLIES", "FOOD LOVERS"),

        // Health
        new("Dis-Chem", "DIS_CHEM", "Health", "fa-solid fa-prescription-bottle-medical", "DIS-CHEM", "DISCHEM", "DIS CHEM"),
        new("Clicks", "CLICKS", "Health", "fa-solid fa-pills", "CLICKS"),
        new("Pharmacy & Medication", "PHARMACY", "Health", "fa-solid fa-prescription-bottle-medical", "PHARMACY", "PHARMA", "APTEEK", "MEDICATION"),
        new("Gym", "GYM", "Health", "fa-solid fa-dumbbell", "VIRGIN ACTIVE", "PLANET FITNESS", "GYM"),

        // Eating out
        new("Mr D Food", "MR_D_FOOD", "Dining", "fa-solid fa-utensils", "MR D FOOD", "MR D", "MRD"),
        new("Uber Eats", "UBER_EATS", "Dining", "fa-solid fa-utensils", "UBER EATS", "UBEREATS"),
        new("Sushi", "YUKI_SUSHI", "Dining", "fa-solid fa-utensils", "SUSHI"),
        new("Nando's", "NANDOS", "Dining", "fa-solid fa-drumstick-bite", "NANDOS", "NANDO S"),
        new("KFC", "KFC", "Dining", "fa-solid fa-drumstick-bite", "KFC"),
        new("McDonald's", "MCDONALDS", "Dining", "fa-solid fa-burger", "MCDONALDS", "MC DONALDS", "MCD"),
        new("Steers", "STEERS", "Dining", "fa-solid fa-burger", "STEERS"),
        new("Wimpy", "WIMPY", "Dining", "fa-solid fa-burger", "WIMPY"),
        new("Spur", "SPUR", "Dining", "fa-solid fa-utensils", "SPUR"),
        new("Debonairs", "DEBONAIRS", "Dining", "fa-solid fa-pizza-slice", "DEBONAIRS"),
        new("Roman's Pizza", "ROMANS_PIZZA", "Dining", "fa-solid fa-pizza-slice", "ROMANS PIZZA"),
        new("Coffee", "COFFEE", "Dining", "fa-solid fa-mug-hot", "STARBUCKS", "VIDA E CAFFE", "MUGG & BEAN", "MUGG AND BEAN", "SEATTLE COFFEE"),

        // Getting around
        new("Uber", "UBER", "Transport", "fa-brands fa-uber", "UBER"),
        new("Bolt", "BOLT", "Transport", "fa-solid fa-taxi", "BOLT.EU", "BOLT"),
        new("Fuel", "FUEL", "Transport", "fa-solid fa-gas-pump", "ENGEN", "SHELL", "SASOL", "CALTEX", "ASTRON", "BP"),

        // Phone, internet and electricity
        new("Vodacom", "VODACOM", "Utilities", "fa-solid fa-mobile-screen", "VODACOM"),
        new("MTN", "MTN", "Utilities", "fa-solid fa-mobile-screen", "MTN"),
        new("Cell C", "CELL_C", "Utilities", "fa-solid fa-mobile-screen", "CELL C", "CELLC"),
        new("Telkom", "TELKOM", "Utilities", "fa-solid fa-phone", "TELKOM"),
        new("Rain", "RAIN", "Utilities", "fa-solid fa-wifi", "RAIN MOBILE", "RAIN.CO.ZA"),
        new("Electricity", "ELECTRICITY", "Utilities", "fa-solid fa-bolt", "ESKOM", "PREPAID ELEC", "PREPAID ELECTRICITY"),

        // Debt and vehicle finance
        new("WesBank Vehicle Finance", "WESBANK", "Debt", "fa-solid fa-car", "WESBANK"),
        new("FNB Credit Card", "FNB_CREDIT_CARD", "Debt", "fa-solid fa-credit-card", "FNBCC", "TRANSFER TO CREDIT"),

        // Shopping
        new("Takealot", "TAKEALOT", "Shopping", "fa-solid fa-box", "TAKEALOT"),
        new("Makro", "MAKRO", "Shopping", "fa-solid fa-store", "MAKRO"),
        new("Mr Price", "MR_PRICE", "Shopping", "fa-solid fa-shirt", "MR PRICE", "MRP"),
        new("Pep", "PEP", "Shopping", "fa-solid fa-shirt", "PEP", "PEP STORES"),
        new("Ackermans", "ACKERMANS", "Shopping", "fa-solid fa-shirt", "ACKERMANS"),
        new("Builders", "BUILDERS", "Shopping", "fa-solid fa-hammer", "BUILDERS WAREHOUSE", "BUILDERS EXPRESS", "BUILDERS"),
        new("Wootware Computers", "WOOTWARE", "Shopping", "fa-solid fa-desktop", "WOOTWARE"),
        new("Tobacconist", "TOBACCONIST", "Entertainment", "fa-solid fa-smoking", "TOBACCONIST"),

        // Between the person's own accounts
        new("Internal Savings Transfer", "INTERNAL_SAVINGS", "Transfer", "fa-solid fa-piggy-bank",
            "INVEST SAVINGS", "PAYMENT TO SAVINGS", "INVESTMENT SAVINGS"),
    ];

    private static readonly HashSet<string> KnownKeys = Known.Select(m => m.Key).ToHashSet(StringComparer.Ordinal);

    /// <summary>Whether the key belongs to a merchant this app recognises, rather than one made from the description.</summary>
    public static bool IsKnownKey(string normalizedKey) => KnownKeys.Contains(normalizedKey);

    public static MerchantInfo Normalize(string rawDescription, TransactionType type)
    {
        var cleaned = CleanDescription(rawDescription);
        var upper = cleaned.ToUpperInvariant();

        KnownMerchant? best = null;
        var bestLength = 0;
        foreach (var merchant in Known)
        {
            if (merchant.Only is { } only && only != type)
            {
                continue;
            }

            foreach (var phrase in merchant.Phrases)
            {
                var length = Matches(upper, phrase) ? phrase.TrimStart('=').Length : 0;
                if (length > bestLength)
                {
                    best = merchant;
                    bestLength = length;
                }
            }
        }

        if (best is not null)
        {
            return new MerchantInfo(best.Name, best.Key, best.Category, best.Icon, best.IsAvoidableFee, best.FeeType);
        }

        // Not a merchant we know: a readable version of the description, and a key made from it.
        var fallbackName = cleaned.Length > 30 ? cleaned[..30] + "…" : cleaned;
        var fallbackKey = upper.Replace(" ", "_");
        if (fallbackKey.Length > 24)
        {
            fallbackKey = fallbackKey[..24];
        }

        return new MerchantInfo(
            fallbackName,
            fallbackKey,
            type == TransactionType.Income ? "Freelance" : "Shopping",
            type == TransactionType.Income ? "fa-solid fa-arrow-down-left" : "fa-solid fa-bag-shopping");
    }

    /// <summary>
    /// Whether the phrase appears as whole words: "SPAR" is in "SPAR SANDTON" but not "AUTO SPARES",
    /// and "MR D" is in "MR D FOOD" but not "MR DIY". "=OF" means the description is exactly "OF".
    /// </summary>
    private static bool Matches(string upper, string phrase)
    {
        if (phrase.StartsWith('='))
        {
            return upper == phrase[1..];
        }

        for (var index = upper.IndexOf(phrase, StringComparison.Ordinal);
             index >= 0;
             index = upper.IndexOf(phrase, index + 1, StringComparison.Ordinal))
        {
            var end = index + phrase.Length;
            var startsWord = index == 0 || !char.IsLetterOrDigit(upper[index - 1]);
            var endsWord = end == upper.Length || !char.IsLetterOrDigit(upper[end]);
            if (startsWord && endsWord)
            {
                return true;
            }
        }

        return false;
    }

    public static string CleanDescription(string description)
    {
        var cleaned = description;

        var match = PosPurchasePattern().Match(cleaned);
        if (match.Success && match.Groups["desc"].Success && !string.IsNullOrWhiteSpace(match.Groups["desc"].Value))
        {
            cleaned = match.Groups["desc"].Value;
        }

        cleaned = LongNumericReferenceRegex().Replace(cleaned, " ");
        cleaned = MultipleWhitespaceRegex().Replace(cleaned, " ").Trim();

        return cleaned.Length == 0 ? description : cleaned;
    }
}
