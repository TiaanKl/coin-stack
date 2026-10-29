using System.Globalization;

namespace CoinStack.Services;

public static class CurrencyFormatting
{
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
        var format = decimals switch
        {
            0 => "C0",
            1 => "C1",
            _ => "C2"
        };

        return amount.ToString(format, CreateCulture(currencyCode));
    }

    public static string GetSymbol(string currencyCode)
    {
        return CreateCulture(currencyCode).NumberFormat.CurrencySymbol;
    }

    private static CultureInfo CreateCulture(string? currencyCode)
    {
        var normalized = (currencyCode ?? "USD").Trim().ToUpperInvariant();

        var (cultureName, currencySymbol) = normalized switch
        {
            "USD" => ("en-US", "$"),
            "EUR" => ("fr-FR", "€"),
            "GBP" => ("en-GB", "£"),
            "CAD" => ("en-CA", "C$"),
            "AUD" => ("en-AU", "A$"),
            "ZAR" => ("en-ZA", "R"),
            _ => ("en-US", "$")
        };

        var culture = (CultureInfo)CultureInfo.GetCultureInfo(cultureName).Clone();
        culture.NumberFormat.CurrencySymbol = currencySymbol;
        // Ensure "." is always the decimal separator so that Blazor's
        // <input type="number"> bindings (which per the HTML spec always
        // use "." regardless of locale) can round-trip without a
        // FormatException in cultures like fr-FR or en-ZA that default
        // to ",".
        culture.NumberFormat.NumberDecimalSeparator = ".";
        culture.NumberFormat.CurrencyDecimalSeparator = ".";
        return culture;
    }
}