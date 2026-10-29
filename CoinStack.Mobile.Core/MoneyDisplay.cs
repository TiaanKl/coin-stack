using CoinStack.Services;

namespace CoinStack.Mobile.Core;

/// <summary>Formats money for the mobile app with the same rules as every other app: -R1 234.56.</summary>
public static class MoneyDisplay
{
    public static string Symbol(string code) => CurrencyFormatting.GetSymbol(code);

    public static string Format(string currencyCode, decimal amount, int decimals = 2)
        => CurrencyFormatting.FormatCurrency(amount, currencyCode, decimals);
}
