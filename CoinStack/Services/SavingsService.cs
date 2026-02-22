using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class SavingsService : ISavingsService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    public SavingsService(IDbContextFactory<CoinStackDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // State
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<SavingsState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SavingsState.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
               ?? new SavingsState();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Monthly calculation
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<SavingsMonthlySummary?> CalculateMonthAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var settings = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                       ?? new AppSettings();

        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");

        // Prevent double-counting
        var state = await db.SavingsState.FirstOrDefaultAsync(cancellationToken);
        if (state is not null && state.LastCalculatedMonth == currentMonth)
        {
            return null;
        }

        // Already have a row for this month? Skip.
        var alreadyRecorded = await db.SavingsMonthlySummaries
            .AnyAsync(x => x.Month == currentMonth, cancellationToken);
        if (alreadyRecorded)
        {
            // Sync LastCalculatedMonth and return null (already done)
            if (state is not null && state.LastCalculatedMonth != currentMonth)
            {
                state.LastCalculatedMonth = currentMonth;
                await db.SaveChangesAsync(cancellationToken);
            }

            return null;
        }

        // Determine base savings
        decimal income = settings.MonthlyIncome;
        decimal baseSavings = settings.SavingsIsPercent
            ? income * (settings.MonthlySavingsPercent / 100m)
            : settings.MonthlySavingsAmount;

        // Get current total for interest calculation
        decimal currentTotal = state?.Total ?? 0m;

        // Calculate interest
        decimal interest = 0m;
        if (settings.SavingsInterestRate.HasValue && settings.SavingsInterestRate.Value > 0)
        {
            var apr = settings.SavingsInterestRate.Value;
            var monthlyRate = settings.SavingsInterestIsYearly ? apr / 12m : apr;
            interest = (currentTotal + baseSavings) * (monthlyRate / 100m);
        }

        var totalAdded = baseSavings + interest;
        var newRunningTotal = currentTotal + totalAdded;

        // Upsert state
        if (state is null)
        {
            state = new SavingsState
            {
                Total = newRunningTotal,
                Available = newRunningTotal,
                Reserved = 0,
                LastCalculatedMonth = currentMonth
            };
            db.SavingsState.Add(state);
        }
        else
        {
            state.Total += totalAdded;
            state.Available += totalAdded;
            state.LastCalculatedMonth = currentMonth;
        }

        // Monthly summary record
        var summary = new SavingsMonthlySummary
        {
            Month = currentMonth,
            Base = baseSavings,
            Interest = interest,
            Total = totalAdded,
            RunningTotal = newRunningTotal
        };
        db.SavingsMonthlySummaries.Add(summary);

        await db.SaveChangesAsync(cancellationToken);
        return summary;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Fallback
    // ──────────────────────────────────────────────────────────────────────────

    public async Task SetFallbackEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var state = await db.SavingsState.FirstOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            state = new SavingsState { FallbackEnabled = enabled };
            db.SavingsState.Add(state);
        }
        else
        {
            state.FallbackEnabled = enabled;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ApplyFallbackAsync(decimal amount, string reason, string sourceName, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var state = await db.SavingsState.FirstOrDefaultAsync(cancellationToken);
        if (state is null || !state.FallbackEnabled || state.Available < amount)
        {
            return false;
        }

        state.Available -= amount;

        var evt = new SavingsFallbackEvent
        {
            OccurredAtUtc = DateTime.UtcNow,
            AmountUsed = amount,
            Reason = reason,
            SourceName = sourceName
        };
        db.SavingsFallbackEvents.Add(evt);

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Queries
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<List<SavingsMonthlySummary>> GetMonthlySummariesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SavingsMonthlySummaries
            .AsNoTracking()
            .OrderByDescending(x => x.Month)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SavingsFallbackEvent>> GetFallbackEventsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SavingsFallbackEvents
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetFallbackUsedThisMonthAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return await db.SavingsFallbackEvents
            .AsNoTracking()
            .Where(x => x.OccurredAtUtc >= startOfMonth)
            .SumAsync(x => x.AmountUsed, cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Projections
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<List<SavingsProjectionPoint>> GetProjectionsAsync(int months = 12, bool includeInterest = true, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var settings = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                       ?? new AppSettings();
        var state = await db.SavingsState.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? new SavingsState();

        decimal income = settings.MonthlyIncome;
        decimal baseMonthly = settings.SavingsIsPercent
            ? income * (settings.MonthlySavingsPercent / 100m)
            : settings.MonthlySavingsAmount;

        decimal apr = settings.SavingsInterestRate ?? 0m;
        decimal monthlyRate = (includeInterest && apr > 0)
            ? (settings.SavingsInterestIsYearly ? apr / 12m / 100m : apr / 100m)
            : 0m;

        var result = new List<SavingsProjectionPoint>(months);
        var running = state.Total;
        var now = DateTime.UtcNow;

        for (var i = 1; i <= months; i++)
        {
            var projectedMonth = now.AddMonths(i);
            var label = projectedMonth.ToString("yyyy-MM");

            var interest = (running + baseMonthly) * monthlyRate;
            running += baseMonthly + interest;

            result.Add(new SavingsProjectionPoint(label, Math.Round(running, 2)));
        }

        return result;
    }
}
