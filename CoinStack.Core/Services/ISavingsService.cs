using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface ISavingsService
{
    /// <summary>The savings and emergency-fund balances, and what goals have set aside.</summary>
    Task<SavingsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the monthly savings for the current month: the planned amount moves from the main account to
    /// savings, and the month's interest is added to savings. No-ops if already done for that month.
    /// </summary>
    Task<SavingsMonthlySummary?> CalculateMonthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly moves up to <paramref name="amount"/> of free savings to the main account for a
    /// force-majeure expense. Does <b>not</b> require <see cref="SavingsState.FallbackEnabled"/>.
    /// Returns the amount actually moved (may be less than requested if savings are insufficient).
    /// Pass <paramref name="transactionId"/> so the withdrawal is undone if that transaction is deleted.
    /// </summary>
    Task<decimal> WithdrawForEmergencyAsync(decimal amount, string reason, int? transactionId = null, CancellationToken cancellationToken = default);

    /// <summary>Enables or disables savings fallback globally.</summary>
    Task SetFallbackEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>Ordered (newest first) monthly history.</summary>
    Task<List<SavingsMonthlySummary>> GetMonthlySummariesAsync(CancellationToken cancellationToken = default);

    /// <summary>Ordered (newest first) fallback events.</summary>
    Task<List<SavingsFallbackEvent>> GetFallbackEventsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the total amount used as fallback in the current calendar month.</summary>
    Task<decimal> GetFallbackUsedThisMonthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a month-by-month projection going <paramref name="months"/> months into the future.
    /// </summary>
    Task<List<SavingsProjectionPoint>> GetProjectionsAsync(int months = 12, bool includeInterest = true, CancellationToken cancellationToken = default);

    /// <summary>Moves money from the main account into savings.</summary>
    Task<ReserveTransferResult> AddToSavingsAsync(decimal amount, string reason, CancellationToken cancellationToken = default);

    /// <summary>Moves money from the main account into the emergency fund.</summary>
    Task<ReserveTransferResult> AddToEmergencyFundAsync(decimal amount, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Covers a shortfall by moving money to the main account: free savings first, then the emergency fund
    /// (if allowed). Returns how much was covered from each.
    /// </summary>
    Task<ReserveCoverageResult> ApplyReserveFallbackAsync(decimal amount, string reason, string sourceName, bool allowEmergencyFund, int? transactionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts back the savings / emergency-fund draws linked to <paramref name="transactionId"/> and removes
    /// those events, returning the amount restored. Automatic shortfall cover is always restored (the game
    /// loop re-applies it after an edit); the user's explicit force-majeure withdrawal only when
    /// <paramref name="includeExplicitWithdrawals"/> is set, i.e. when the transaction is deleted.
    /// </summary>
    Task<decimal> RestoreForTransactionAsync(int transactionId, bool includeExplicitWithdrawals = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// A single month on the savings projection curve.
/// </summary>
/// <param name="Month">The calendar month label in <c>yyyy-MM</c> form.</param>
/// <param name="Projected">The projected closing balance for the month.</param>
/// <param name="Contributions">Cumulative capital paid in (opening balance plus deposits) by this month.</param>
/// <param name="InterestEarned">Cumulative interest earned by this month.</param>
public sealed record SavingsProjectionPoint(
    string Month,
    decimal Projected,
    decimal Contributions = 0m,
    decimal InterestEarned = 0m);
/// <summary>
/// Savings now. <paramref name="SavingsBalance"/> is the savings account, of which goals have set aside
/// <paramref name="Reserved"/>; the rest is free to use. <paramref name="EmergencyBalance"/> is the
/// emergency-fund account.
/// </summary>
public sealed record SavingsSnapshot(
    decimal SavingsBalance,
    decimal Reserved,
    decimal EmergencyBalance,
    bool FallbackEnabled,
    string? LastCalculatedMonth)
{
    /// <summary>Savings not set aside for goals.</summary>
    public decimal Available => Math.Max(0m, SavingsBalance - Reserved);
}

public sealed record ReserveTransferResult(decimal SavingsAdded, decimal EmergencyAdded);
public sealed record ReserveCoverageResult(decimal SavingsUsed, decimal EmergencyUsed)
{
    public decimal TotalCovered => SavingsUsed + EmergencyUsed;
}
