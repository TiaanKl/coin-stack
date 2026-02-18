using FinanceManager.Data.Entities;

namespace FinanceManager.Services;

public interface IGameLoopService
{
    Task<GameState> GetCurrentStateAsync(CancellationToken cancellationToken = default);
    Task ProcessDailyCheckInAsync(CancellationToken cancellationToken = default);
    Task<GameTransactionResult> ProcessTransactionAsync(Transaction transaction, int userTimezoneOffsetHours, CancellationToken cancellationToken = default);
    Task RevertTransactionImpactAsync(Transaction originalTransaction, CancellationToken cancellationToken);
}

public sealed class GameState
{
    public int TotalScore { get; set; }
    public List<ScoreEvent> RecentEvents { get; set; } = [];
    public List<Streak> Streaks { get; set; } = [];
    public Reflection? PendingReflection { get; set; }
    public List<BucketStatus> BucketStatuses { get; set; } = [];
}

public sealed class BucketStatus
{
    public int BucketId { get; set; }
    public string Name { get; set; } = "";
    public string? ColorHex { get; set; }
    public decimal Allocated { get; set; }
    public decimal Spent { get; set; }
    public decimal Remaining => Allocated - Spent;
    public double PercentUsed => Allocated > 0 ? (double)(Spent / Allocated) * 100 : 0;
    public bool IsOverBudget => Spent > Allocated;
}

public sealed class GameTransactionResult
{
    public int PointsChanged { get; set; }
    public string? Message { get; set; }
    public bool TriggeredReflection { get; set; }
    public Reflection? Reflection { get; set; }
}
