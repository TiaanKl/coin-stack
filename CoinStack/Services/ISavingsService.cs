using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface ISavingsService
{
    /// <summary>Gets or creates the singleton savings state.</summary>
    Task<SavingsState> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the monthly savings calculation for the current month.
    /// No-ops if already calculated for that month.
    /// </summary>
    Task<SavingsMonthlySummary?> CalculateMonthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly withdraws up to <paramref name="amount"/> from available savings for a force-majeure
    /// expense. Does <b>not</b> require <see cref="SavingsState.FallbackEnabled"/>.
    /// Returns the amount actually deducted (may be less than requested if savings are insufficient).
    /// </summary>
    Task<decimal> WithdrawForEmergencyAsync(decimal amount, string reason, CancellationToken cancellationToken = default);

    /// <summary>Enables or disables savings fallback globally.</summary>
    Task SetFallbackEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deducts <paramref name="amount"/> from available savings as a fallback and logs the event.
    /// Returns false if savings are insufficient or fallback is disabled.
    /// </summary>
    Task<bool> ApplyFallbackAsync(decimal amount, string reason, string sourceName, CancellationToken cancellationToken = default);

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

    /// <summary>Adds funds from available budget into savings.</summary>
    Task<ReserveTransferResult> AddToSavingsAsync(decimal amount, string reason, CancellationToken cancellationToken = default);

    /// <summary>Adds funds from available budget into emergency funds.</summary>
    Task<ReserveTransferResult> AddToEmergencyFundAsync(decimal amount, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies reserve fallback in order: savings first, then emergency fund (if enabled).
    /// Returns how much was covered from each source.
    /// </summary>
    Task<ReserveCoverageResult> ApplyReserveFallbackAsync(decimal amount, string reason, string sourceName, bool allowEmergencyFund, CancellationToken cancellationToken = default);
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
public sealed record ReserveTransferResult(decimal SavingsAdded, decimal EmergencyAdded);
public sealed record ReserveCoverageResult(decimal SavingsUsed, decimal EmergencyUsed)
{
    public decimal TotalCovered => SavingsUsed + EmergencyUsed;
}
