using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class SavingsService : ISavingsService
{
    /// <summary>Marks a withdrawal the user chose explicitly (force majeure), as opposed to automatic shortfall cover.</summary>
    internal const string EmergencyWithdrawalSource = "Emergency Withdrawal";

    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;
    private readonly TimeProvider _clock;

    public SavingsService(IDbContextFactory<CoinStackDbContext> dbFactory, TimeProvider? clock = null)
    {
        _dbFactory = dbFactory;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<SavingsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await GetSnapshotAsync(db, cancellationToken);
    }

    /// <summary>
    /// Savings now: the balances of the savings and emergency-fund accounts (the first open one of each,
    /// which is where savings moves money), and how much of savings goals have set aside.
    /// </summary>
    internal static async Task<SavingsSnapshot> GetSnapshotAsync(CoinStackDbContext db, CancellationToken cancellationToken)
    {
        var balances = await AccountService.GetBalancesAsync(db, cancellationToken);
        var state = await db.SavingsState.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? new SavingsState();

        return new SavingsSnapshot(
            FirstBalance(balances, AccountType.Savings),
            state.Reserved,
            FirstBalance(balances, AccountType.EmergencyFund),
            state.FallbackEnabled,
            state.LastCalculatedMonth);
    }

    public async Task<SavingsMonthlySummary?> CalculateMonthAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var settings = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                       ?? new AppSettings();

        var now = _clock.GetUtcNow().UtcDateTime;
        var currentMonth = _clock.Today().ToString("yyyy-MM");

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

        // Nothing is set aside from an overdrawn account; the Savings page says so.
        if (await AccountService.GetSpendableBalanceAsync(db, cancellationToken) < 0m)
        {
            return null;
        }

        decimal income = settings.MonthlyIncome;
        decimal baseSavings = settings.SavingsIsPercent
            ? income * (settings.MonthlySavingsPercent / 100m)
            : settings.MonthlySavingsAmount;

        // Interest is earned on everything in the savings account, including what is set aside for goals.
        var main = await AccountService.GetOrCreateDefaultAsync(db, cancellationToken);
        var savingsAccount = await AccountService.GetOrCreateOfTypeAsync(db, AccountType.Savings, now, cancellationToken);
        var currentBalance = (await GetSnapshotAsync(db, cancellationToken)).SavingsBalance;

        decimal interest = 0m;
        if (settings.SavingsInterestRate.HasValue && settings.SavingsInterestRate.Value > 0)
        {
            // Rate is stored as a fraction (0.07 = 7%); one month of the annual nominal rate accrues
            // on the balance including this month's deposit.
            var annualRate = GetAnnualRateFraction(settings, includeInterest: true);
            var monthlyRate = annualRate / 12m;
            interest = FinancialEngine.RoundCurrency((currentBalance + baseSavings) * monthlyRate);
        }

        // This month's saving leaves the main account for savings.
        if (baseSavings > 0m)
        {
            AccountService.AddTransfer(db, main.Id, savingsAccount.Id, baseSavings, now,
                $"Monthly savings {currentMonth}", TransactionConventions.AutoSource);
        }

        // Interest grows the savings balance without being recorded as income, so it doesn't count as
        // earnings in budgets, reports, recaps or the forecast. The monthly summary keeps it.
        savingsAccount.Balance += interest;

        if (state is null)
        {
            state = new SavingsState { LastCalculatedMonth = currentMonth };
            db.SavingsState.Add(state);
        }
        else
        {
            state.LastCalculatedMonth = currentMonth;
        }

        var summary = new SavingsMonthlySummary
        {
            Month = currentMonth,
            Base = baseSavings,
            Interest = interest,
            Total = baseSavings + interest,
            // The balance after this month: what is in the savings account, goal money included.
            RunningTotal = currentBalance + baseSavings + interest
        };
        db.SavingsMonthlySummaries.Add(summary);

        await db.SaveChangesAsync(cancellationToken);
        return summary;
    }

    public async Task<decimal> WithdrawForEmergencyAsync(decimal amount, string reason, int? transactionId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var withdrawn = Math.Min(Math.Max(0m, amount), (await GetSnapshotAsync(db, cancellationToken)).Available);
        if (withdrawn <= 0m)
        {
            return 0m;
        }

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await DrawToMainAsync(db, AccountType.Savings, withdrawn, reason, EmergencyWithdrawalSource, transactionId, cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return withdrawn;
    }

    public Task<ReserveTransferResult> AddToSavingsAsync(decimal amount, string reason, CancellationToken cancellationToken = default)
        => MoveFromMainAsync(AccountType.Savings, amount, string.IsNullOrWhiteSpace(reason) ? "Moved to savings" : reason, cancellationToken);

    public Task<ReserveTransferResult> AddToEmergencyFundAsync(decimal amount, string reason, CancellationToken cancellationToken = default)
        => MoveFromMainAsync(AccountType.EmergencyFund, amount, string.IsNullOrWhiteSpace(reason) ? "Moved to the emergency fund" : reason, cancellationToken);

    /// <summary>A transfer from the main account into savings or the emergency fund.</summary>
    private async Task<ReserveTransferResult> MoveFromMainAsync(
        AccountType type,
        decimal amount,
        string description,
        CancellationToken cancellationToken)
    {
        var normalizedAmount = Math.Max(0m, amount);
        if (normalizedAmount == 0m)
        {
            return new ReserveTransferResult(0m, 0m);
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var now = _clock.GetUtcNow().UtcDateTime;
        var main = await AccountService.GetOrCreateDefaultAsync(db, cancellationToken);
        var target = await AccountService.GetOrCreateOfTypeAsync(db, type, now, cancellationToken);

        AccountService.AddTransfer(db, main.Id, target.Id, normalizedAmount, now, description);
        await db.SaveChangesAsync(cancellationToken);

        return type == AccountType.EmergencyFund
            ? new ReserveTransferResult(0m, normalizedAmount)
            : new ReserveTransferResult(normalizedAmount, 0m);
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

        var snapshot = await GetSnapshotAsync(db, cancellationToken);
        if (!snapshot.FallbackEnabled)
        {
            return new ReserveCoverageResult(0m, 0m);
        }

        // Free savings first, then the emergency fund; money set aside for goals is left alone.
        var savingsUsed = Math.Min(normalizedAmount, snapshot.Available);
        var emergencyUsed = allowEmergencyFund
            ? Math.Min(normalizedAmount - savingsUsed, Math.Max(0m, snapshot.EmergencyBalance))
            : 0m;

        if (savingsUsed <= 0m && emergencyUsed <= 0m)
        {
            return new ReserveCoverageResult(0m, 0m);
        }

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (savingsUsed > 0m)
        {
            await DrawToMainAsync(db, AccountType.Savings, savingsUsed, reason,
                string.IsNullOrWhiteSpace(sourceName) ? "Savings" : sourceName, transactionId, cancellationToken);
        }

        if (emergencyUsed > 0m)
        {
            await DrawToMainAsync(db, AccountType.EmergencyFund, emergencyUsed, reason, "Emergency Fund", transactionId, cancellationToken);
        }

        await dbTransaction.CommitAsync(cancellationToken);
        return new ReserveCoverageResult(savingsUsed, emergencyUsed);
    }

    /// <summary>
    /// Moves money from savings or the emergency fund to the main account to cover spending, and logs the
    /// draw with the transfer that made it, so undoing the draw removes the transfer.
    /// </summary>
    private async Task DrawToMainAsync(
        CoinStackDbContext db,
        AccountType from,
        decimal amount,
        string reason,
        string sourceName,
        int? transactionId,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var main = await AccountService.GetOrCreateDefaultAsync(db, cancellationToken);
        var source = await AccountService.GetOrCreateOfTypeAsync(db, from, now, cancellationToken);

        var transfer = AccountService.AddTransfer(db, source.Id, main.Id, amount, now, reason, TransactionConventions.AutoSource);
        await db.SaveChangesAsync(cancellationToken);

        db.SavingsFallbackEvents.Add(new SavingsFallbackEvent
        {
            OccurredAtUtc = now,
            AmountUsed = amount,
            Reason = reason,
            SourceName = sourceName,
            FromEmergencyFund = from == AccountType.EmergencyFund,
            TransactionId = transactionId,
            TransferTransactionId = transfer.Id
        });
        await db.SaveChangesAsync(cancellationToken);
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

    /// <summary>
    /// Undoes the draws linked to a transaction: each draw's transfer is deleted, which puts the money back.
    /// A draw from before accounts existed only lowered a figure, so undoing it raises the account's balance
    /// the same way. Shared by the service and by <see cref="TransactionService"/> on delete. The caller saves.
    /// </summary>
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

        if (events.Count == 0)
        {
            return 0m;
        }

        foreach (var evt in events)
        {
            if (evt.TransferTransactionId is { } transferId)
            {
                var transfer = await db.Transactions.FirstOrDefaultAsync(t => t.Id == transferId, cancellationToken);
                if (transfer is not null)
                {
                    db.Transactions.Remove(transfer);
                }
            }
            else
            {
                var account = await AccountService.GetOrCreateOfTypeAsync(
                    db, evt.FromEmergencyFund ? AccountType.EmergencyFund : AccountType.Savings, DateTime.UtcNow, cancellationToken);
                account.Balance += evt.AmountUsed;
            }
        }

        db.SavingsFallbackEvents.RemoveRange(events);
        return events.Sum(e => e.AmountUsed);
    }

    private static decimal FirstBalance(List<AccountBalance> balances, AccountType type)
        => balances
            .Where(b => b.Account.Type == type && !b.Account.IsArchived)
            .OrderBy(b => b.Account.Id)
            .Select(b => b.Balance)
            .FirstOrDefault();

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
        var today = _clock.Today();
        var startOfMonth = _clock.StartOfDayUtc(new DateTime(today.Year, today.Month, 1));
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
        var snapshot = await GetSnapshotAsync(db, cancellationToken);

        decimal income = settings.MonthlyIncome;
        decimal baseMonthly = settings.SavingsIsPercent
            ? income * (settings.MonthlySavingsPercent / 100m)
            : settings.MonthlySavingsAmount;

        // SavingsInterestRate is persisted as a fraction (0.07 = 7%). Convert a monthly-quoted
        // rate to its annual nominal equivalent so the engine always receives an annual fraction.
        var annualRate = GetAnnualRateFraction(settings, includeInterest);

        var result = new List<SavingsProjectionPoint>(months);
        // Project from what is in the savings account now, goal money included.
        var openingBalance = snapshot.SavingsBalance;
        var now = _clock.Today();

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
