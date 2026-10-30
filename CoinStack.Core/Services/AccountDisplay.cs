using CoinStack.Data.Entities;

namespace CoinStack.Services;

/// <summary>How each kind of account is named and drawn, so every screen shows them the same way.</summary>
public static class AccountDisplay
{
    public static string Label(AccountType type) => type switch
    {
        AccountType.Cheque => "Cheque",
        AccountType.Savings => "Savings",
        AccountType.EmergencyFund => "Emergency fund",
        AccountType.CreditCard => "Credit card",
        _ => type.ToString()
    };

    /// <summary>A Font Awesome solid icon name.</summary>
    public static string Icon(AccountType type) => type switch
    {
        AccountType.Cheque => "fa-building-columns",
        AccountType.Savings => "fa-piggy-bank",
        AccountType.EmergencyFund => "fa-life-ring",
        AccountType.CreditCard => "fa-credit-card",
        _ => "fa-wallet"
    };

    /// <summary>The last four digits, so a full account number isn't shown on screen.</summary>
    public static string? MaskedNumber(string? accountNumber)
    {
        var digits = AccountService.AccountNumberDigits(accountNumber);
        return digits is null ? null : digits.Length <= 4 ? digits : $"•••• {digits[^4..]}";
    }
}
