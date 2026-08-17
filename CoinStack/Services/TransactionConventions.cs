using CoinStack.Data.Entities;

namespace CoinStack.Services;

/// <summary>
/// Shared rules for telling real bank activity apart from app-generated placeholders.
/// </summary>
public static class TransactionConventions
{
    public const string SyntheticMonthlyIncomeDescription = "Monthly Income";
    public const string BankSource = "FNB";
    public const string AutoSource = "Auto";

    public static bool IsSyntheticMonthlyIncome(Transaction transaction)
    {
        return transaction.Type == TransactionType.Income
               && string.Equals(transaction.Description, SyntheticMonthlyIncomeDescription, StringComparison.OrdinalIgnoreCase)
               && !string.Equals(transaction.Source, BankSource, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBankImported(Transaction transaction)
    {
        return string.Equals(transaction.Source, BankSource, StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeSalary(string description)
    {
        return description.Contains("SALARY", StringComparison.OrdinalIgnoreCase);
    }
}
