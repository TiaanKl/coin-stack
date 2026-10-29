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
/// Intelligent merchant and service normalizer for bank transaction descriptions.
/// Maps variants (e.g., "Onlyfans*J", "Of", "Onlyfans.Com*") to canonical merchants ("OnlyFans"),
/// and identifies service icons, categories, pass-throughs, and avoidable bank fees.
/// </summary>
public static partial class MerchantNormalizer
{
    [GeneratedRegex(@"(?:POS\s+Purchase\s+)?(?:\d+(?:\.\d+)?\s+)?(?<desc>.*?)(?:\s+\d{6}\*\d{4}.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex PosPurchasePattern();

    [GeneratedRegex(@"\b\d{10,}\b")]
    private static partial Regex LongNumericReferenceRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleWhitespaceRegex();

    public static MerchantInfo Normalize(string rawDescription, TransactionType type)
    {
        var cleaned = CleanDescription(rawDescription);
        var upper = cleaned.ToUpperInvariant();

        // 1. Bank fees and avoidable charges
        if (upper.Contains("#ITEM PAID NO FUNDS") || upper.Contains("ITEM PAID NO FUNDS"))
        {
            return new MerchantInfo(
                "FNB Unpaid Item Fee",
                "FNB_UNPAID_FEE",
                "Banking Fees",
                "fa-solid fa-triangle-exclamation",
                IsAvoidableFee: true,
                FeeType: "Unpaid Item (Insufficient Funds)");
        }

        if (upper.Contains("#DEBIT CARD POS UNSUCCESSFUL") || upper.Contains("DECLINED PURCH") || upper.Contains("DECLINED FOREIGN"))
        {
            return new MerchantInfo(
                "FNB Declined Transaction Fee",
                "FNB_DECLINED_FEE",
                "Banking Fees",
                "fa-solid fa-circle-xmark",
                IsAvoidableFee: true,
                FeeType: "Declined Card Fee");
        }

        if (upper.Contains("#SERVICE FEES") || upper.Contains("PAYMENTS BUNDLE FEE") || upper.Contains("SERVICE FEES #INT PYMT"))
        {
            return new MerchantInfo(
                "FNB Service & Foreign Fees",
                "FNB_SERVICE_FEES",
                "Banking Fees",
                "fa-solid fa-building-columns");
        }

        if (upper.Contains("MONTHLY ACCOUNT FEE") || upper.Contains("MONTHLYACCOUNTFEE"))
        {
            return new MerchantInfo(
                "FNB Monthly Account Fee",
                "FNB_ACCOUNT_FEE",
                "Banking Fees",
                "fa-solid fa-building-columns");
        }

        // 2. Salary & Inflow Sources
        if (type == TransactionType.Income)
        {
            if (upper.Contains("SALARY") || upper.Contains("MULTICAT SALARY"))
            {
                return new MerchantInfo(
                    "Salary (Multicat)",
                    "SALARY",
                    "Salary",
                    "fa-solid fa-money-bill-wave");
            }

            if (upper.Contains("MULTICAT"))
            {
                return new MerchantInfo(
                    "Multicat Payment",
                    "MULTICAT_PAYMENT",
                    "Freelance",
                    "fa-solid fa-briefcase");
            }

            if (upper.Contains("PAYMENT FROM PA") || upper.Contains("FROM PA PNP"))
            {
                return new MerchantInfo(
                    "Payment from Pa",
                    "FAMILY_PA",
                    "Personal",
                    "fa-solid fa-user-group");
            }

            if (upper.Contains("PAYMENT FROM MA") || upper.Contains("FROM MA"))
            {
                return new MerchantInfo(
                    "Payment from Ma",
                    "FAMILY_MA",
                    "Personal",
                    "fa-solid fa-user-group");
            }

            if (upper.Contains("PAYSHAP CREDIT") || upper.Contains("HOLTSHOUSEN"))
            {
                return new MerchantInfo(
                    "PayShap Transfer",
                    "PAYSHAP_IN",
                    "Freelance",
                    "fa-solid fa-bolt");
            }

            if (upper.Contains("REFUND") || upper.Contains("REVERSAL"))
            {
                return new MerchantInfo(
                    "Refund / Fee Reversal",
                    "REFUND",
                    "Freelance",
                    "fa-solid fa-rotate-left");
            }
        }

        // 3. Digital Subscriptions & Services (Group OnlyFans / OF, Steam, etc.)
        if (upper.Contains("ONLYFANS") || upper.Equals("OF") || upper.StartsWith("OF ") || upper.EndsWith(" OF") || upper.Contains(" OF "))
        {
            return new MerchantInfo(
                "OnlyFans",
                "ONLYFANS",
                "Entertainment",
                "fa-solid fa-heart");
        }

        if (upper.Contains("STEAM") || upper.Contains("STEAMGAMES"))
        {
            return new MerchantInfo(
                "Steam",
                "STEAM",
                "Entertainment",
                "fa-brands fa-steam");
        }

        if (upper.Contains("GOOGLE"))
        {
            return new MerchantInfo(
                "Google Play & Services",
                "GOOGLE",
                "Entertainment",
                "fa-brands fa-google");
        }

        if (upper.Contains("MICROSOFT") || upper.Contains("XBOX"))
        {
            return new MerchantInfo(
                "Microsoft",
                "MICROSOFT",
                "Entertainment",
                "fa-brands fa-microsoft");
        }

        if (upper.Contains("GUMROAD"))
        {
            return new MerchantInfo(
                "Gumroad",
                "GUMROAD",
                "Entertainment",
                "fa-solid fa-bag-shopping");
        }

        if (upper.Contains("PATREON"))
        {
            return new MerchantInfo(
                "Patreon",
                "PATREON",
                "Entertainment",
                "fa-brands fa-patreon");
        }

        if (upper.Contains("IONOS"))
        {
            return new MerchantInfo(
                "IONOS Hosting",
                "IONOS",
                "Utilities",
                "fa-solid fa-server");
        }

        if (upper.Contains("RUNPOD"))
        {
            return new MerchantInfo(
                "RunPod GPU",
                "RUNPOD",
                "Utilities",
                "fa-solid fa-microchip");
        }

        if (upper.Contains("CURSOR"))
        {
            return new MerchantInfo(
                "Cursor AI",
                "CURSOR",
                "Utilities",
                "fa-solid fa-code");
        }

        if (upper.Contains("OPENAI"))
        {
            return new MerchantInfo(
                "OpenAI",
                "OPENAI",
                "Utilities",
                "fa-solid fa-robot");
        }

        if (upper.Contains("OPENROUTER"))
        {
            return new MerchantInfo(
                "OpenRouter",
                "OPENROUTER",
                "Utilities",
                "fa-solid fa-network-wired");
        }

        if (upper.Contains("TENSORDOCK"))
        {
            return new MerchantInfo(
                "TensorDock",
                "TENSORDOCK",
                "Utilities",
                "fa-solid fa-server");
        }

        if (upper.Contains("COMFY.ORG") || upper.Contains("COMFYORG"))
        {
            return new MerchantInfo(
                "ComfyOrg",
                "COMFYORG",
                "Entertainment",
                "fa-solid fa-palette");
        }

        if (upper.Contains("WEMOD"))
        {
            return new MerchantInfo(
                "WeMod",
                "WEMOD",
                "Entertainment",
                "fa-solid fa-gamepad");
        }

        if (upper.Contains("GITHUB"))
        {
            return new MerchantInfo(
                "GitHub",
                "GITHUB",
                "Utilities",
                "fa-brands fa-github");
        }

        if (upper.Contains("PAYPAL"))
        {
            return new MerchantInfo(
                "PayPal",
                "PAYPAL",
                "Entertainment",
                "fa-brands fa-paypal");
        }

        if (upper.Contains("GAME CREDITS") || upper.Contains("COMEWELCOM") || upper.Contains("TOTEM CORE"))
        {
            return new MerchantInfo(
                "Gaming & In-App Purchases",
                "GAMING_CREDITS",
                "Entertainment",
                "fa-solid fa-dice");
        }

        // 4. Retail & Groceries
        if (upper.Contains("PICK N PAY") || upper.Contains("PNP"))
        {
            return new MerchantInfo(
                "Pick n Pay",
                "PICK_N_PAY",
                "Groceries",
                "fa-solid fa-basket-shopping");
        }

        if (upper.Contains("SPAR"))
        {
            return new MerchantInfo(
                "Spar",
                "SPAR",
                "Groceries",
                "fa-solid fa-basket-shopping");
        }

        if (upper.Contains("OK MINI MARK") || upper.Contains("OK MINIMARK") || upper.Contains("CHECKERS") || upper.Contains("SHOPRITE") || upper.Contains("WOOLWORTHS"))
        {
            return new MerchantInfo(
                "Groceries & Convenience",
                "GROCERIES_OTHER",
                "Groceries",
                "fa-solid fa-cart-shopping");
        }

        // 5. Health & Pharmacy
        if (upper.Contains("DIS-CHEM") || upper.Contains("DISCHEM"))
        {
            return new MerchantInfo(
                "Dis-Chem",
                "DIS_CHEM",
                "Health",
                "fa-solid fa-prescription-bottle-medical");
        }

        if (upper.Contains("CLICKS"))
        {
            return new MerchantInfo(
                "Clicks",
                "CLICKS",
                "Health",
                "fa-solid fa-pills");
        }

        if (upper.Contains("UITZICHT PHARMA") || upper.Contains("PHARMA") || upper.Contains("MEDICATION"))
        {
            return new MerchantInfo(
                "Pharmacy & Medication",
                "PHARMACY",
                "Health",
                "fa-solid fa-prescription-bottle-medical");
        }

        // 6. Food & Dining
        if (upper.Contains("MR D FOOD") || upper.Contains("MR D"))
        {
            return new MerchantInfo(
                "Mr D Food",
                "MR_D_FOOD",
                "Dining",
                "fa-solid fa-utensils");
        }

        if (upper.Contains("YUKI SUSHI") || upper.Contains("SUSHI"))
        {
            return new MerchantInfo(
                "Yuki Sushi",
                "YUKI_SUSHI",
                "Dining",
                "fa-solid fa-utensils");
        }

        // 7. Debt & Vehicle Finance
        if (upper.Contains("WESBANK"))
        {
            return new MerchantInfo(
                "WesBank Vehicle Finance",
                "WESBANK",
                "Debt",
                "fa-solid fa-car");
        }

        if (upper.Contains("FNBCC") || upper.Contains("TRANSFER TO CREDIT"))
        {
            return new MerchantInfo(
                "FNB Credit Card",
                "FNB_CREDIT_CARD",
                "Debt",
                "fa-solid fa-credit-card");
        }

        // 8. Shopping & Personal
        if (upper.Contains("WOOTWARE"))
        {
            return new MerchantInfo(
                "Wootware Computers",
                "WOOTWARE",
                "Shopping",
                "fa-solid fa-desktop");
        }

        if (upper.Contains("TOBACCONIST"))
        {
            return new MerchantInfo(
                "T's Tobacconist & Vape",
                "TOBACCONIST",
                "Entertainment",
                "fa-solid fa-smoking");
        }

        if (upper.Contains("PAYMENT TO PA") || upper.Contains("TO PA TIAAN"))
        {
            return new MerchantInfo(
                "Payment to Pa",
                "FAMILY_PA_OUT",
                "Personal",
                "fa-solid fa-user-group");
        }

        if (upper.Contains("PAYMENT TO MARCEL") || upper.Contains("TO MARCEL"))
        {
            return new MerchantInfo(
                "Payment to Marcel",
                "MARCEL_OUT",
                "Personal",
                "fa-solid fa-user");
        }

        if (upper.Contains("PAYSHAP") || upper.Contains("AURORA"))
        {
            return new MerchantInfo(
                "PayShap: Aurora",
                "PAYSHAP_AURORA",
                "Personal",
                "fa-solid fa-paper-plane");
        }

        if (upper.Contains("INVEST SAVINGS") || upper.Contains("PAYMENT TO SAVINGS") || upper.Contains("INVESTMENT SAVINGS"))
        {
            return new MerchantInfo(
                "Internal Savings Transfer",
                "INTERNAL_SAVINGS",
                "Transfer",
                "fa-solid fa-piggy-bank");
        }

        // Default fallback: clean readable merchant description
        var fallbackName = cleaned;
        if (fallbackName.Length > 30)
        {
            fallbackName = fallbackName[..30] + "…";
        }

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
