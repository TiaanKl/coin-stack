using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class SavingsService : ISavingsService
{
    /// <summary>Marks a withdrawal the user chose explicitly (force majeure), as opposed to automatic shortfall cover.</summary>
    internal const string EmergencyWithdrawalSource = "Emergency Withdrawal";

    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    public SavingsService(IDbContextFactory<CoinStackDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<SavingsState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SavingsState.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
               ?? new SavingsState();
    }

    public async Task<SavingsMonthlySummary?> CalculateMonthAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var settings = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                       ?? new AppSettings();

        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");

        var state = await db.SavingsState.FirstOrDefaultAsync(cancellationToken);
        if (state is not null && state.LastCalculatedMonth == currentMonth)
        {
            return null;
        }

        var alreadyRecorded = await db.SavingsMonthlySummaries
            .AnyAsync(x => x.Month == currentMonth, cancellationToken);

        if (alreadyRecorded)
        {
            if (state is not null && state.LastCalculatedMonth != currentMonth)
            {
                state.LastCalculatedMonth = currentMonth;
                await db.SaveChangesAsync(cancellationToken);
            }

            return null;
        }

        if (settings.BankBalanceAsOfUtc is not null && settings.CurrentBankBalance < 0m)
        {
            return null;
        }

        decimal income = settings.MonthlyIncome;
        decimal baseSavings = settings.SavingsIsPercent
            ? income * (settings.MonthlySavingsPercent / 100m)
            : settings.MonthlySavingsAmount;

        // Interest is earned on the money still in savings, including what is set aside for goals
        // (Reserved). Total is everything ever paid in and never goes down, so it would overstate it.
        decimal currentBalance = (state?.Available ?? 0m) + (state?.Reserved ?? 0m);

        decimal interest = 0m;
        if (settings.SavingsInterestRate.HasValue && settings.SavingsInterestRate.Value > 0)
        {
            // Rate is stored as a fraction (0.07 = 7%); one month of the annual nominal rate accrues
            // on the balance including this month's deposit.
            var annualRate = GetAnnualRateFraction(settings, includeInterest: true);
            var monthlyRate = annualRate / 12m;
            interest = FinancialEngine.RoundCurrency((currentBalance + baseSavings) * monthlyRate);
        }

        var totalAdded = baseSavings + interest;

        if (state is null)
        {
            state = new SavingsState
            {
                Total = totalAdded,
                Available = totalAdded,
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

        var summary = new SavingsMonthlySummary
        {
            Month = currentMonth,
            Base = baseSavings,
            Interest = interest,
            Total = totalAdded,
            // The balance after this month: what is actually in savings, goal money included.
            RunningTotal = state.Available + state.Reserved
        };
        db.SavingsMonthlySummaries.Add(summary);

        await db.SaveChangesAsync(cancellationToken);
        return summary;
    }

    public async Task<decimal> WithdrawForEmergencyAsync(decimal amount, string reason, int? transactionId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var state = await db.SavingsState.FirstOrDefaultAsync(cancellationToken);
        if (state is null || state.Available <= 0)
        {
            return 0m;
        }

        var withdrawn = Math.Min(amount, state.Available);
        state.Available -= withdrawn;

        db.SavingsFallbackEvents.Add(new SavingsFallbackEvent
        {
            OccurredAtUtc = DateTime.UtcNow,
            AmountUsed = withdrawn,
            Reason = reason,
            SourceName = EmergencyWithdrawalSource,
            TransactionId = transactionId
        });

        await db.SaveChangesAsync(cancellationToken);
        return withdrawn;
    }

    public async Task<ReserveTransferResult> AddToSavingsAsync(decimal amount, string reason, CancellationToken cancellationToken = default)
    {
        var normalizedAmount = Math.Max(0m, amount);
        if (normalizedAmount == 0m)
        {
            return new ReserveTransferResult(0m, 0m);
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var state = await db.SavingsState.FirstOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            state = new SavingsState();
            db.SavingsState.Add(state);
        }

        state.Total += normalizedAmount;
        state.Available += normalizedAmount;

        await db.SaveChangesAsync(cancellationToken);
        return new ReserveTransferResult(normalizedAmount, 0m);
    }

    public async Task<ReserveTransferResult> AddToEmergencyFundAsync(decimal amount, string reason, CancellationToken cancellationToken = default)
    {
        var normalizedAmount = Math.Max(0m, amount);
        if (normalizedAmount == 0m)
        {
            return new ReserveTransferResult(0m, 0m);
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var state = await db.SavingsState.FirstOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            state = new SavingsState();
            db.SavingsState.Add(state);
        }

        state.EmergencyTotal += normalizedAmount;
        state.EmergencyAvailable += normalizedAmount;

        await db.SaveChangesAsync(cancellationToken);
        return new ReserveTransferResult(0m, normalizedAmount);
    }

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
        var result = await ApplyReserveFallbackAsync(amount, reason, sourceName, allowEmergencyFund: false, cancellationToken: cancellationToken);
        return result.TotalCovered >= amount;
    }

    public async Task<ReserveCoverageResult> ApplyReserveFallbackAsync(
        decimal amount,
        string reason,
        string sourceName,
        bool allowEmergencyFund,
        int? transactionId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedAmount = Math.Max(0m, amount);
        if (normalizedAmount == 0m)
        {
            return new ReserveCoverageResult(0m, 0m);
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var state = await db.SavingsState.FirstOrDefaultAsync(cancellationToken);
        if (state is null || !state.FallbackEnabled)
        {
            return new ReserveCoverageResult(0m, 0m);
        }

        var remaining = normalizedAmount;
        var savingsUsed = Math.Min(remaining, state.Available);
        if (savingsUsed > 0m)
        {
            state.Available -= savingsUsed;
            remaining -= savingsUsed;

            db.SavingsFallbackEvents.Add(new SavingsFallbackEvent
            {
                OccurredAtUtc = DateTime.UtcNow,
                AmountUsed = savingsUsed,
                Reason = reason,
                SourceName = string.IsNullOrWhiteSpace(sourceName) ? "Savings" : sourceName,
                TransactionId = transactionId
            });
        }

        var emergencyUsed = 0m;
        if (allowEmergencyFund && remaining > 0m)
        {
            emergencyUsed = Math.Min(remaining, state.EmergencyAvailable);
            if (emergencyUsed > 0m)
            {
                state.EmergencyAvailable -= emergencyUsed;
                remaining -= emergencyUsed;

                db.SavingsFallbackEvents.Add(new SavingsFallbackEvent
                {
                    OccurredAtUtc = DateTime.UtcNow,
                    AmountUsed = emergencyUsed,
                    Reason = reason,
                    SourceName = "Emergency Fund",
                    FromEmergencyFund = true,
                    TransactionId = transactionId
                });
            }
        }

        if (savingsUsed > 0m || emergencyUsed > 0m)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new ReserveCoverageResult(savingsUsed, emergencyUsed);
    }

    public async Task<decimal> RestoreForTransactionAsync(
        int transactionId,
        bool includeExplicitWithdrawals = false,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var restored = await RestoreForTransactionAsync(db, transactionId, includeExplicitWithdrawals, cancellationToken);
        if (restored > 0m)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return restored;
    }

    /// <summary>Shared by the service and by <see cref="TransactionService"/> on delete. The caller saves.</summary>
    internal static async Task<decimal> RestoreForTransactionAsync(
        CoinStackDbContext db,
        int transactionId,
        bool includeExplicitWithdrawals,
        CancellationToken cancellationToken)
    {
        var events = await db.SavingsFallbackEvents
            .Where(e => e.TransactionId == transactionId
                        && (includeExplicitWithdrawals || e.SourceName != EmergencyWithdrawalSource))
            .ToListAsync(cancellationToken);

        var state = await db.SavingsState.FirstOrDefaultAsync(cancellationToken);
        if (events.Count == 0 || state is null)
        {
            return 0m;
        }

        foreach (var evt in events)
        {
            if (evt.FromEmergencyFund)
            {
                state.EmergencyAvailable += evt.AmountUsed;
            }
            else
            {
                state.Available += evt.AmountUsed;
            }
        }

        db.SavingsFallbackEvents.RemoveRange(events);
        return events.Sum(e => e.AmountUsed);
    }

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
            .Where(x => x.OccurredAtUtc >= startOfMonth && x.AmountUsed > 0)
            .SumAsync(x => x.AmountUsed, cancellationToken);
    }

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

        // SavingsInterestRate is persisted as a fraction (0.07 = 7%). Convert a monthly-quoted
        // rate to its annual nominal equivalent so the engine always receives an annual fraction.
        var annualRate = GetAnnualRateFraction(settings, includeInterest);

        var result = new List<SavingsProjectionPoint>(months);
        // Project from what is in savings now (goal money included); Total still counts withdrawals.
        var openingBalance = state.Available + state.Reserved;
        var now = DateTime.UtcNow;

        for (var i = 1; i <= months; i++)
        {
            var label = now.AddMonths(i).ToString("yyyy-MM");

            var projection = FinancialEngine.CalculateSavingsProjection(
                openingBalance,
                baseMonthly,
                annualRate,
                i);

            result.Add(new SavingsProjectionPoint(
                label,
                projection.FutureValue,
                projection.TotalContributions,
                projection.TotalInterestEarned));
        }

        return result;
    }

    /// <summary>
    /// Resolves the configured savings interest rate to an annual nominal fraction suitable for
    /// <see cref="FinancialEngine"/>.
    /// </summary>
    /// <param name="settings">The persisted application settings.</param>
    /// <param name="includeInterest">When <see langword="false"/> the rate is forced to zero.</param>
    /// <returns>The annual nominal rate as a fraction, or zero when interest is disabled.</returns>
    private static decimal GetAnnualRateFraction(AppSettings settings, bool includeInterest)
    {
        var rate = settings.SavingsInterestRate ?? 0m;

        if (!includeInterest || rate <= 0m)
        {
            return 0m;
        }

        return settings.SavingsInterestIsYearly ? rate : rate * 12m;
    }
}
