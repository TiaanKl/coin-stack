using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

/// <summary>
/// Savings goals. Money put towards a goal comes out of savings and is held as
/// <see cref="SavingsState.Reserved"/> (still savings, but set aside), so it can't also be spent as
/// shortfall cover; withdrawing, abandoning or deleting a goal puts it back.
/// </summary>
public sealed class GoalService : IGoalService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;
    private readonly IAchievementService? _achievementService;

    public GoalService(IDbContextFactory<CoinStackDbContext> dbFactory, IAchievementService? achievementService = null)
    {
        _dbFactory = dbFactory;
        _achievementService = achievementService;
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
        existing.TargetDateUtc = goal.TargetDateUtc;

        if (existing.Status != GoalStatus.Abandoned)
        {
            existing.Status = existing.CurrentAmount >= existing.TargetAmount ? GoalStatus.Completed : GoalStatus.Active;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<GoalMoneyResult> ContributeAsync(int goalId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0m)
        {
            return GoalMoneyResult.Failed("Enter an amount greater than zero.", amount);
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var goal = await db.Goals.FirstOrDefaultAsync(x => x.Id == goalId, cancellationToken);
        if (goal is null)
        {
            return GoalMoneyResult.Failed("That goal no longer exists.", amount);
        }

        if (goal.Status != GoalStatus.Active)
        {
            return GoalMoneyResult.Failed($"This goal is {goal.Status.ToString().ToLowerInvariant()}; only active goals take contributions.", amount);
        }

        var stillNeeded = goal.TargetAmount - goal.CurrentAmount;
        if (stillNeeded <= 0m)
        {
            return GoalMoneyResult.Failed("This goal is already fully funded.", amount);
        }

        var toMove = Math.Min(amount, stillNeeded);
        var savings = await db.SavingsState.FirstOrDefaultAsync(cancellationToken);
        var available = savings?.Available ?? 0m;
        if (savings is null || available < toMove)
        {
            return GoalMoneyResult.Failed(
                $"Only {Math.Max(0m, available):N2} is free in savings. Add money to savings first, or contribute less.",
                amount);
        }

        savings.Available -= toMove;
        savings.Reserved += toMove;
        goal.CurrentAmount += toMove;

        var completed = goal.CurrentAmount >= goal.TargetAmount;
        if (completed)
        {
            goal.Status = GoalStatus.Completed;
        }

        await db.SaveChangesAsync(cancellationToken);

        if (completed && _achievementService is not null)
        {
            await _achievementService.EvaluateAsync(cancellationToken);
        }

        return new GoalMoneyResult(toMove, amount - toMove, completed);
    }

    public async Task<GoalMoneyResult> WithdrawAsync(int goalId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0m)
        {
            return GoalMoneyResult.Failed("Enter an amount greater than zero.", amount);
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var goal = await db.Goals.FirstOrDefaultAsync(x => x.Id == goalId, cancellationToken);
        if (goal is null)
        {
            return GoalMoneyResult.Failed("That goal no longer exists.", amount);
        }

        var moved = await ReturnToSavingsAsync(db, goal, amount, cancellationToken);
        if (goal.Status == GoalStatus.Completed && goal.CurrentAmount < goal.TargetAmount)
        {
            goal.Status = GoalStatus.Active;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new GoalMoneyResult(moved, amount - moved, false);
    }

    public async Task<GoalMoneyResult> AbandonAsync(int goalId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var goal = await db.Goals.FirstOrDefaultAsync(x => x.Id == goalId, cancellationToken);
        if (goal is null)
        {
            return GoalMoneyResult.Failed("That goal no longer exists.", 0m);
        }

        var moved = await ReturnToSavingsAsync(db, goal, goal.CurrentAmount, cancellationToken);
        goal.Status = GoalStatus.Abandoned;
        await DetachBucketsAsync(db, goal.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return new GoalMoneyResult(moved, 0m, false);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Goals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        await ReturnToSavingsAsync(db, existing, existing.CurrentAmount, cancellationToken);
        await DetachBucketsAsync(db, existing.Id, cancellationToken);

        db.Goals.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Takes up to <paramref name="amount"/> out of the goal and back into available savings. Money that
    /// was in the goal before goals drew on savings (a starting amount) has no reservation to release,
    /// so <see cref="SavingsState.Reserved"/> never goes below zero. The caller saves.
    /// </summary>
    private static async Task<decimal> ReturnToSavingsAsync(
        CoinStackDbContext db,
        Goal goal,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var moved = Math.Min(amount, goal.CurrentAmount);
        if (moved <= 0m)
        {
            return 0m;
        }

        var savings = await db.SavingsState.FirstOrDefaultAsync(cancellationToken);
        if (savings is null)
        {
            savings = new SavingsState();
            db.SavingsState.Add(savings);
        }

        savings.Reserved = Math.Max(0m, savings.Reserved - moved);
        savings.Available += moved;
        goal.CurrentAmount -= moved;
        return moved;
    }

    /// <summary>Savings buckets that fed this goal stop doing so; they stay savings buckets.</summary>
    private static async Task DetachBucketsAsync(CoinStackDbContext db, int goalId, CancellationToken cancellationToken)
    {
        var buckets = await db.Buckets.Where(b => b.GoalId == goalId).ToListAsync(cancellationToken);
        foreach (var bucket in buckets)
        {
            bucket.GoalId = null;
        }
    }
}
