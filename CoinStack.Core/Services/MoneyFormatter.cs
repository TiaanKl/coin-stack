namespace CoinStack.Services;

public sealed class MoneyFormatter : IMoneyFormatter
{
    public string Symbol(string currencyCode) => currencyCode switch
    {
        "USD" => "$",
        "EUR" => "€",
        "GBP" => "£",
        "CAD" => "C$",
        "AUD" => "A$",
        "ZAR" => "R",
        _ => "$"
    };

    public string Format(string currencyCode, decimal amount, int decimals = 2)
    {
        var symbol = Symbol(currencyCode);
        var precision = decimals < 0 ? 0 : decimals;
        return $"{symbol}{amount.ToString($"N{precision}")}";
    }
}
