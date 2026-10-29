namespace CoinStack.Services;

/// <summary>Injectable wrapper over <see cref="CurrencyFormatting"/>, so every screen formats money identically.</summary>
public sealed class MoneyFormatter : IMoneyFormatter
{
    public string Symbol(string currencyCode) => CurrencyFormatting.GetSymbol(currencyCode);

    public string Format(string currencyCode, decimal amount, int decimals = 2)
        => CurrencyFormatting.FormatCurrency(amount, currencyCode, decimals);
}
