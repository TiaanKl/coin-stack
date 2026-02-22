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
}

public sealed record SavingsProjectionPoint(string Month, decimal Projected);
