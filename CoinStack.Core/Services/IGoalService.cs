using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface IGoalService
{
    Task<List<Goal>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Goal?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Goal> CreateAsync(Goal goal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the name, target and date. The amount saved only changes through
    /// <see cref="ContributeAsync"/> and <see cref="WithdrawAsync"/>, so money is always accounted for.
    /// </summary>
    Task UpdateAsync(Goal goal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves money from savings into the goal, up to what the goal still needs. Nothing moves if
    /// savings can't cover it.
    /// </summary>
    Task<GoalMoneyResult> ContributeAsync(int goalId, decimal amount, CancellationToken cancellationToken = default);

    /// <summary>Moves money from the goal back into savings.</summary>
    Task<GoalMoneyResult> WithdrawAsync(int goalId, decimal amount, CancellationToken cancellationToken = default);

    /// <summary>Returns everything in the goal to savings and marks it abandoned.</summary>
    Task<GoalMoneyResult> AbandonAsync(int goalId, CancellationToken cancellationToken = default);

    /// <summary>Returns everything in the goal to savings, then deletes it.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>The outcome of moving money into or out of a goal.</summary>
/// <param name="Moved">The amount that actually moved.</param>
/// <param name="NotMoved">The part of the request that didn't move (e.g. more than the goal still needed).</param>
/// <param name="Error">Why nothing moved, or null on success.</param>
public sealed record GoalMoneyResult(decimal Moved, decimal NotMoved, bool GoalCompleted, string? Error = null)
{
    public bool Succeeded => Error is null;

    public static GoalMoneyResult Failed(string error, decimal requested) => new(0m, requested, false, error);
}
