using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

/// <summary>
/// Debts. A debt linked to a credit-card account follows the card: its balance is what the card owes, a
/// payment is a transfer to the card, and its monthly interest is a charge on the card.
/// </summary>
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
        var debts = await db.DebtAccounts
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        await ApplyCardBalancesAsync(db, debts, cancellationToken);
        return debts;
    }

    public async Task<DebtAccount?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var debt = await db.DebtAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (debt is not null)
        {
            await ApplyCardBalancesAsync(db, [debt], cancellationToken);
        }

        return debt;
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

        // A linked debt's balance is the card's; it changes through the card account, not here.
        var followsCard = await db.Accounts.AnyAsync(a => a.DebtAccountId == debt.Id, cancellationToken);

        // A balance typed in by the user is a statement balance "as of today": interest up to now is in it.
        if (!followsCard && existing.CurrentBalance != debt.CurrentBalance)
        {
            existing.InterestAccruedThroughUtc = DateTime.UtcNow.Date;
            existing.CurrentBalance = debt.CurrentBalance;
        }

        existing.Name = debt.Name;
        existing.Provider = debt.Provider;
        existing.TotalAmount = debt.TotalAmount;
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

        // Paying a card moves money from the main account to the card; it isn't spending.
        if (await db.Accounts.FirstOrDefaultAsync(a => a.DebtAccountId == debtId, cancellationToken) is { } card)
        {
            var main = await AccountService.GetOrCreateDefaultAsync(db, cancellationToken);
            AccountService.AddTransfer(db, main.Id, card.Id, amount, DateTime.UtcNow, $"{existing.Name} payment");
            await db.SaveChangesAsync(cancellationToken);
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
        var cards = await db.Accounts.AsNoTracking().Where(a => a.DebtAccountId != null).ToListAsync(cancellationToken);
        var cardBalances = cards.Count == 0
            ? []
            : (await AccountService.GetBalancesAsync(db, cancellationToken)).ToDictionary(b => b.Account.Id, b => b.Balance);
        var debtCategoryId = cards.Count == 0
            ? null
            : await db.Categories.Where(c => c.Name == "Debt").Select(c => (int?)c.Id).FirstOrDefaultAsync(cancellationToken);

        var utcNow = DateTime.UtcNow;
        var total = 0m;
        foreach (var debt in debts)
        {
            var card = cards.FirstOrDefault(c => c.DebtAccountId == debt.Id);
            if (card is null)
            {
                total += DebtInterestAccrual.Accrue(debt, utcNow);
                continue;
            }

            // Interest on a card is worked out on what the card owes, and charged to the card.
            debt.CurrentBalance = -cardBalances.GetValueOrDefault(card.Id);
            var interest = DebtInterestAccrual.Accrue(debt, utcNow);
            if (interest > 0m)
            {
                db.Transactions.Add(new Transaction
                {
                    Type = TransactionType.Expense,
                    AccountId = card.Id,
                    Amount = interest,
                    Description = $"Interest on {debt.Name}",
                    CategoryId = debtCategoryId,
                    ExpenseKind = ExpenseKind.Mandatory,
                    Source = TransactionConventions.AutoSource,
                    OccurredAtUtc = utcNow
                });
                total += interest;
            }
        }

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

    /// <summary>A debt linked to a card owes what the card does: a card at -R1 200 is a debt of R1 200.</summary>
    private static async Task ApplyCardBalancesAsync(CoinStackDbContext db, IReadOnlyCollection<DebtAccount> debts, CancellationToken cancellationToken)
    {
        var cards = await db.Accounts.AsNoTracking().Where(a => a.DebtAccountId != null).ToListAsync(cancellationToken);
        if (cards.Count == 0)
        {
            return;
        }

        var balances = (await AccountService.GetBalancesAsync(db, cancellationToken)).ToDictionary(b => b.Account.Id, b => b.Balance);
        foreach (var debt in debts)
        {
            if (cards.FirstOrDefault(c => c.DebtAccountId == debt.Id) is { } card)
            {
                debt.CurrentBalance = -balances.GetValueOrDefault(card.Id);
            }
        }
    }
}
