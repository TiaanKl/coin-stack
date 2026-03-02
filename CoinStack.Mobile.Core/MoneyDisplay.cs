namespace CoinStack.Mobile.Core;

public static class MoneyDisplay
{
    public static string Symbol(string code) => code switch
    {
        "USD" => "$",
        "EUR" => "€",
        "GBP" => "£",
        "CAD" => "C$",
        "AUD" => "A$",
        "ZAR" => "R",
        _ => "$"
    };

    public static string Format(string currencyCode, decimal amount, int decimals = 2)
    {
        var precision = decimals < 0 ? 0 : decimals;
        return $"{Symbol(currencyCode)}{amount.ToString($"N{precision}")}";
    }
}
