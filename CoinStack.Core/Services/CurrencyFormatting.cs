using System.Collections.Concurrent;
using System.Globalization;
using CoinStack.Data.Entities;

namespace CoinStack.Services;

/// <summary>
/// The one way money is formatted. Every currency maps to a fixed culture, so the symbol, digit
/// grouping and minus sign are the same on every page: -R1 234.56, never R-1 234.56 or R1,234.56.
/// </summary>
public static class CurrencyFormatting
{
    private static readonly ConcurrentDictionary<string, CultureInfo> Cultures = new(StringComparer.Ordinal);

    public static void ApplyCurrency(string currencyCode)
    {
        var culture = CreateCulture(currencyCode);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public static string FormatCurrency(decimal amount, string currencyCode, int decimals = 2)
    {
        var precision = Math.Clamp(decimals, 0, 4);
        return amount.ToString("C" + precision.ToString(CultureInfo.InvariantCulture), CreateCulture(currencyCode));
    }

    public static string GetSymbol(string currencyCode)
    {
        return CreateCulture(currencyCode).NumberFormat.CurrencySymbol;
    }

    private static CultureInfo CreateCulture(string? currencyCode)
    {
        var normalized = (currencyCode ?? AppSettings.DefaultCurrency).Trim().ToUpperInvariant();
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
        // Ensure "." is always the decimal separator so that Blazor's
        // <input type="number"> bindings (which per the HTML spec always
        // use "." regardless of locale) can round-trip without a
        // FormatException in cultures like fr-FR or en-ZA that default
        // to ",".
        format.NumberDecimalSeparator = ".";
        format.CurrencyDecimalSeparator = ".";
        format.PercentDecimalSeparator = ".";
        // Minus sign in front of the whole amount: -R1 234.56 (prefix symbol) or -1 234.56 € (suffix symbol).
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
