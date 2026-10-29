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

    /// <summary>
    /// Matches only the placeholder rows the old auto-income feature generated: exact description,
    /// stamped at midnight UTC on the period start, with no category, bucket, notes or import link.
    /// A real income the user happened to name "Monthly Income" is not matched.
    /// </summary>
    public static bool IsSyntheticMonthlyIncome(Transaction transaction)
    {
        return transaction.Type == TransactionType.Income
               && string.Equals(transaction.Description, SyntheticMonthlyIncomeDescription, StringComparison.Ordinal)
               && !string.Equals(transaction.Source, BankSource, StringComparison.OrdinalIgnoreCase)
               && transaction.CategoryId is null
               && transaction.BucketId is null
               && transaction.StatementImportId is null
               && string.IsNullOrEmpty(transaction.Notes)
               && transaction.OccurredAtUtc.TimeOfDay == TimeSpan.Zero;
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
