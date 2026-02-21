using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class GoalService : IGoalService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    public GoalService(IDbContextFactory<CoinStackDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Goal>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Goals
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Goal?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Goals
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Goal> CreateAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.Goals.Add(goal);
        await db.SaveChangesAsync(cancellationToken);
        return goal;
    }

    public async Task UpdateAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.Goals.FirstOrDefaultAsync(x => x.Id == goal.Id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.Name = goal.Name;
        existing.TargetAmount = goal.TargetAmount;
        existing.CurrentAmount = goal.CurrentAmount;
        existing.TargetDateUtc = goal.TargetDateUtc;
        existing.Status = goal.Status;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Goals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        db.Goals.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }
}
