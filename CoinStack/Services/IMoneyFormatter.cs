namespace CoinStack.Services;

public interface IMoneyFormatter
{
    string Symbol(string currencyCode);
    string Format(string currencyCode, decimal amount, int decimals = 2);
}
