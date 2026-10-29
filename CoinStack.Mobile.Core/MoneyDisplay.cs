using System.Collections.Concurrent;
using System.Globalization;

namespace CoinStack.Mobile.Core;

/// <summary>
/// Formats money for the mobile app. Mirrors <c>CoinStack.Services.CurrencyFormatting</c> (this project
/// does not reference CoinStack.Core), so both apps show -R1 234.56 and never R-1 234.56.
/// </summary>
public static class MoneyDisplay
{
    private static readonly ConcurrentDictionary<string, CultureInfo> Cultures = new(StringComparer.Ordinal);

    public static string Symbol(string code) => CultureFor(code).NumberFormat.CurrencySymbol;

    public static string Format(string currencyCode, decimal amount, int decimals = 2)
    {
        var precision = Math.Clamp(decimals, 0, 4);
        return amount.ToString("C" + precision.ToString(CultureInfo.InvariantCulture), CultureFor(currencyCode));
    }

    private static CultureInfo CultureFor(string? currencyCode)
    {
        var normalized = (currencyCode ?? "ZAR").Trim().ToUpperInvariant();
        return Cultures.GetOrAdd(normalized, BuildCulture);
    }

    private static CultureInfo BuildCulture(string currencyCode)
    {
        var (cultureName, currencySymbol) = currencyCode switch
        {
            "USD" => ("en-US", "$"),
            "EUR" => ("fr-FR", "€"),
            "GBP" => ("en-GB", "£"),
            "CAD" => ("en-CA", "C$"),
            "AUD" => ("en-AU", "A$"),
            _ => ("en-ZA", "R")
        };

        var culture = (CultureInfo)CultureInfo.GetCultureInfo(cultureName).Clone();
        var format = culture.NumberFormat;
        format.CurrencySymbol = currencySymbol;
        format.NumberDecimalSeparator = ".";
        format.CurrencyDecimalSeparator = ".";
        format.PercentDecimalSeparator = ".";
        format.CurrencyNegativePattern = format.CurrencyPositivePattern switch
        {
            1 => 5,
            2 => 9,
            3 => 8,
            _ => 1
        };

        return CultureInfo.ReadOnly(culture);
    }
}
