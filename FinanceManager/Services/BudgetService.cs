using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class BudgetService : IBudgetService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    public BudgetService(IDbContextFactory<CoinStackDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Budget>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Budgets
            .AsNoTracking()
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ToListAsync(cancellationToken);
    }

    public async Task<Budget?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Budgets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Budget> CreateAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.Budgets.Add(budget);
        await db.SaveChangesAsync(cancellationToken);
        return budget;
    }

    public async Task UpdateAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.Budgets.FirstOrDefaultAsync(x => x.Id == budget.Id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.Year = budget.Year;
        existing.Month = budget.Month;
        existing.LimitAmount = budget.LimitAmount;
        existing.CategoryId = budget.CategoryId;
        existing.BucketId = budget.BucketId;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Budgets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        db.Budgets.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }
}
