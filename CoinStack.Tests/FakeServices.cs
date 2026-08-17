using CoinStack.Data.Entities;
using CoinStack.Services;

namespace CoinStack.Tests;

internal sealed class FakeSettingsService : ISettingsService
{
    private AppSettings _settings;

    public event Action? SettingsChanged;

    public FakeSettingsService(AppSettings settings)
    {
        _settings = settings;
    }

    public Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_settings);
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        _settings = settings;
        SettingsChanged?.Invoke();
        return Task.CompletedTask;
    }
}

internal sealed class FakeGameLoopService : IGameLoopService
{
    public int ProcessTransactionCalls { get; private set; }
    public int RevertCalls { get; private set; }

    public GameTransactionResult NextResult { get; set; } = new()
    {
        PointsChanged = 5,
        Message = "ok"
    };

    public Task<GameState> GetCurrentStateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GameState());
    }

    public Task ProcessDailyCheckInAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<GameTransactionResult> ProcessTransactionAsync(Transaction transaction, int userTimezoneOffsetHours, CancellationToken cancellationToken = default)
    {
        ProcessTransactionCalls++;
        return Task.FromResult(NextResult);
    }

    public Task RevertTransactionImpactAsync(Transaction originalTransaction, CancellationToken cancellationToken)
    {
        RevertCalls++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeBucketService : IBucketService
{
    private readonly Dictionary<int, Bucket> _buckets = [];

    public Dictionary<int, decimal> SpentAmountsForPeriod { get; } = [];

    public void Add(Bucket bucket)
    {
        _buckets[bucket.Id] = bucket;
    }

    public Task<List<Bucket>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_buckets.Values.ToList());
    }

    public Task<Bucket?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        _buckets.TryGetValue(id, out var bucket);
        return Task.FromResult(bucket);
    }

    public Task<Bucket> CreateAsync(Bucket bucket, CancellationToken cancellationToken = default)
    {
        var nextId = _buckets.Count == 0 ? 1 : _buckets.Keys.Max() + 1;
        bucket.Id = nextId;
        _buckets[bucket.Id] = bucket;
        return Task.FromResult(bucket);
    }

    public Task UpdateAsync(Bucket bucket, CancellationToken cancellationToken = default)
    {
        _buckets[bucket.Id] = bucket;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        _buckets.Remove(id);
        return Task.CompletedTask;
    }

    public Task<Dictionary<int, decimal>> GetSpentAmountsAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new Dictionary<int, decimal>(SpentAmountsForPeriod));
    }

    public Task<Dictionary<int, decimal>> GetSpentAmountsForPeriodAsync(int monthStartDay, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new Dictionary<int, decimal>(SpentAmountsForPeriod));
    }
}

internal sealed class FakeScoringService : IScoringService
{
    private int _totalScore;

    public int EvaluateCalls { get; private set; }
    public int AddScoreEventCalls { get; private set; }

    public Task<int> GetTotalScoreAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_totalScore);
    }

    public Task<List<ScoreEvent>> GetRecentEventsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<List<ScoreEvent>>([]);
    }

    public Task<ScoreEvent> AddScoreEventAsync(int points, ScoreChangeReason reason, string description, int? transactionId = null, int? bucketId = null, CancellationToken cancellationToken = default)
    {
        AddScoreEventCalls++;
        _totalScore += points;
        return Task.FromResult(new ScoreEvent
        {
            Points = points,
            Reason = reason,
            Description = description,
            TransactionId = transactionId,
            BucketId = bucketId,
        });
    }

    public Task EvaluateTransactionAsync(Transaction transaction, decimal bucketAllocated, decimal bucketSpentBefore, CancellationToken cancellationToken = default)
    {
        EvaluateCalls++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeReflectionService : IReflectionService
{
    private int _nextReflectionId = 1;

    public int CreateCalls { get; private set; }
    public ReflectionTrigger? LastTrigger { get; private set; }

    public Task<Reflection> CreateReflectionAsync(ReflectionTrigger trigger, int? transactionId = null, CancellationToken cancellationToken = default)
    {
        CreateCalls++;
        LastTrigger = trigger;

        return Task.FromResult(new Reflection
        {
            Id = _nextReflectionId++,
            Trigger = trigger,
            TransactionId = transactionId,
            Prompt = "Test prompt",
        });
    }

    public Task CompleteReflectionAsync(int reflectionId, string response, int moodBefore, int moodAfter, EmotionTag? emotionTag = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<List<Reflection>> GetRecentAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<List<Reflection>>([]);
    }

    public Task<Reflection?> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Reflection?>(null);
    }

    public Task<List<EmotionSpendPattern>> GetEmotionPatternsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<List<EmotionSpendPattern>>([]);
    }
}

internal sealed class FakeLevelService : ILevelService
{
    public int AddXpCalls { get; private set; }
    public int LastXpAmount { get; private set; }

    public Task<UserLevel> GetCurrentLevelAsync(CancellationToken ct = default)
        => Task.FromResult(new UserLevel { Id = 1, Level = 1, CurrentXp = 0, TotalXp = 0 });

    public Task<UserLevel> AddXpAsync(int xpAmount, CancellationToken ct = default)
    {
        AddXpCalls++;
        LastXpAmount = xpAmount;
        return Task.FromResult(new UserLevel { Id = 1, Level = 1, CurrentXp = xpAmount, TotalXp = xpAmount });
    }

    public int GetXpRequiredForLevel(int level) => 50;
    public string GetTitleForLevel(int level) => "Novice";
    public Task<bool> CheckLevelUpAsync(CancellationToken ct = default) => Task.FromResult(false);
}

internal sealed class FakeTransactionService : ITransactionService
{
    public int CreateWithGameLoopCalls { get; private set; }

    public Task<List<Transaction>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<List<Transaction>>([]);

    public Task<List<Transaction>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        => Task.FromResult<List<Transaction>>([]);

    public Task<List<Transaction>> GetFromDateAsync(DateTime fromUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<List<Transaction>>([]);

    public Task<Transaction?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult<Transaction?>(null);

    public Task<Transaction> CreateAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        transaction.Id = 1;
        return Task.FromResult(transaction);
    }

    public Task<(Transaction Transaction, GameTransactionResult Result)> CreateWithGameLoopAsync(
        Transaction transaction,
        int userTimezoneOffsetHours,
        CancellationToken cancellationToken = default)
    {
        CreateWithGameLoopCalls++;
        transaction.Id = 1;
        return Task.FromResult((transaction, new GameTransactionResult
        {
            PointsChanged = -5,
            Message = "Impulse purchase recorded",
            Kind = FeedbackKind.Negative,
        }));
    }

    public Task UpdateAsync(Transaction transaction, int userTimezoneOffsetHours = 0, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<decimal> GetExpenseTotalForBudgetPeriodAsync(int monthStartDay, DateTime utcNow, CancellationToken cancellationToken = default)
        => Task.FromResult(0m);

    public Task<(decimal TotalIncome, decimal TotalExpense)> GetLifetimeTotalsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult((0m, 0m));

    public Task<decimal> GetNetBalanceBeforeAsync(DateTime beforeUtc, CancellationToken cancellationToken = default)
        => Task.FromResult(0m);

    public Task<(decimal Income, decimal Expense)> GetIncomeExpenseForPeriodAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default)
        => Task.FromResult((0m, 0m));

    public Task<List<BucketSpendSummary>> GetBucketSpendingForPeriodAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default)
        => Task.FromResult<List<BucketSpendSummary>>([]);

    public Task<List<DailyNetSummary>> GetDailyNetForRangeAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default)
        => Task.FromResult<List<DailyNetSummary>>([]);

    public Task ApplyAutoDeductionsForBudgetPeriodAsync(int monthStartDay, DateTime utcNow, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<int> RemoveSyntheticMonthlyIncomeAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}
