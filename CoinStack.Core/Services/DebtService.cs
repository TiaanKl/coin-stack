using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class DebtService : IDebtService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    public DebtService(IDbContextFactory<CoinStackDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<DebtAccount>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DebtAccounts
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<DebtAccount?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DebtAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<DebtAccount> CreateAsync(DebtAccount debt, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        debt.InterestAccruedThroughUtc ??= DateTime.UtcNow.Date;
        db.DebtAccounts.Add(debt);
        await db.SaveChangesAsync(cancellationToken);
        return debt;
    }

    public async Task UpdateAsync(DebtAccount debt, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.DebtAccounts.FirstOrDefaultAsync(x => x.Id == debt.Id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        // A balance typed in by the user is a statement balance "as of today": interest up to now is in it.
        if (existing.CurrentBalance != debt.CurrentBalance)
        {
            existing.InterestAccruedThroughUtc = DateTime.UtcNow.Date;
        }

        existing.Name = debt.Name;
        existing.Provider = debt.Provider;
        existing.TotalAmount = debt.TotalAmount;
        existing.CurrentBalance = debt.CurrentBalance;
        existing.MonthlyPaymentAmount = debt.MonthlyPaymentAmount;
        existing.InterestRatePercent = debt.InterestRatePercent;
        existing.PaymentStartDateUtc = debt.PaymentStartDateUtc.Date;
        existing.PlannedTermMonths = debt.PlannedTermMonths;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordPaymentAsync(int debtId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.DebtAccounts.FirstOrDefaultAsync(x => x.Id == debtId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        DebtInterestAccrual.Accrue(existing, DateTime.UtcNow);
        existing.CurrentBalance = Math.Max(0m, existing.CurrentBalance - amount);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<decimal> AccrueInterestAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var debts = await db.DebtAccounts.ToListAsync(cancellationToken);

        var utcNow = DateTime.UtcNow;
        var total = debts.Sum(debt => DebtInterestAccrual.Accrue(debt, utcNow));

        await db.SaveChangesAsync(cancellationToken);
        return total;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.DebtAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        db.DebtAccounts.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }
}