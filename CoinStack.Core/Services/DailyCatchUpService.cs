using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoinStack.Services;

/// <summary>
/// The once-a-day work: auto-deductions, debt interest, waitlist cool-offs, challenges and weekly recaps.
/// It used to run on page loads (twice with prerendering). Now each app calls <see cref="RunIfDueAsync"/>
/// when it opens or comes back to the front, and the web app also once a day while it stays open. The
/// first call on a new day on the device's calendar does the work; the rest return straight away.
/// The daily check-in is not part of it: that rewards the person opening the app, not the clock.
/// </summary>
public interface IDailyCatchUpService
{
    /// <summary>Runs the catch-up unless it already ran today. Returns whether it ran.</summary>
    Task<bool> RunIfDueAsync(CancellationToken cancellationToken = default);
}

public sealed class DailyCatchUpService : IDailyCatchUpService
{
    /// <summary>How far back auto-deductions are filled in after a long break.</summary>
    internal const int MaxPeriodsToCatchUp = 12;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime? _lastRunDay;

    public DailyCatchUpService(IServiceScopeFactory scopeFactory, TimeProvider? clock = null)
    {
        _scopeFactory = scopeFactory;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<bool> RunIfDueAsync(CancellationToken cancellationToken = default)
    {
        var today = _clock.Today();
        if (_lastRunDay == today)
        {
            return false;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;
            await using var db = await services.GetRequiredService<IDbContextFactory<CoinStackDbContext>>()
                .CreateDbContextAsync(cancellationToken);

            var settings = await db.AppSettings.FirstOrDefaultAsync(cancellationToken);
            if (settings is null)
            {
                settings = new AppSettings();
                db.AppSettings.Add(settings);
            }

            // Persisted, so a restart later the same day doesn't run it again.
            if (settings.LastCatchUpUtc is { } lastRun && _clock.LocalDate(lastRun) == today)
            {
                _lastRunDay = today;
                return false;
            }

            var now = _clock.GetUtcNow().UtcDateTime;

            await services.GetRequiredService<IBankStatementImportService>().ReconcileAppWithBankDataAsync(cancellationToken);

            var transactions = services.GetRequiredService<ITransactionService>();
            foreach (var period in PeriodsToFill(settings.MonthStartDay, settings.LastCatchUpUtc, now))
            {
                await transactions.ApplyAutoDeductionsForBudgetPeriodAsync(settings.MonthStartDay, period.StartUtc, cancellationToken);
            }

            await services.GetRequiredService<IDebtService>().AccrueInterestAsync(cancellationToken);
            await services.GetRequiredService<IWaitlistService>().EvaluateCoolOffsAsync(cancellationToken);

            var challenges = services.GetRequiredService<IDailyChallengeService>();
            await challenges.ExpireOldChallengesAsync(cancellationToken);
            await challenges.GenerateDailyChallengesAsync(cancellationToken);

            await services.GetRequiredService<IWeeklyRecapService>().GenerateMissingRecapsAsync(ct: cancellationToken);

            settings.LastCatchUpUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            _lastRunDay = today;
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The pay periods to fill in, oldest first: every one since the last run, or only the current one the
    /// first time (the app can't know what happened before it was used).
    /// </summary>
    internal IEnumerable<BudgetPeriod> PeriodsToFill(int payday, DateTime? lastRunUtc, DateTime nowUtc)
    {
        var current = BudgetPeriod.Containing(payday, nowUtc, _clock.LocalTimeZone);
        var periods = new List<BudgetPeriod> { current };

        if (lastRunUtc is { } lastRun)
        {
            var period = current;
            while (periods.Count < MaxPeriodsToCatchUp && !period.Contains(lastRun) && period.StartUtc > lastRun)
            {
                period = period.Previous;
                periods.Add(period);
            }
        }

        periods.Reverse();
        return periods;
    }
}
