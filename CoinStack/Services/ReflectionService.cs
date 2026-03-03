using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class ReflectionService : IReflectionService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    private static readonly Dictionary<ReflectionTrigger, string[]> Prompts = new()
    {
        [ReflectionTrigger.OverBudgetSpend] =
        [
            "What were you feeling right before this purchase?",
            "Was this a need or a want? What made it feel urgent?",
            "If you could go back 10 minutes, would you still buy this?",
            "What could you do differently next time you feel the urge to overspend?"
        ],
        [ReflectionTrigger.SavingsDip] =
        [
            "What situation led you to dip into savings?",
            "How does it feel knowing your savings decreased?",
            "What's one small step you can take to rebuild this amount?",
            "Is there a pattern to when you dip into savings?"
        ],
        [ReflectionTrigger.LargeExpense] =
        [
            "Did you plan for this expense, or was it unexpected?",
            "How does this purchase align with your financial goals?",
            "Rate how necessary this was on a scale of 1-10. Why?",
            "What emotions influenced this spending decision?"
        ],
        [ReflectionTrigger.ImpulseBuy] =
        [
            "What triggered the impulse to buy this?",
            "How long did you think about it before purchasing?",
            "What would have happened if you waited 24 hours?",
            "Can you identify the emotion behind this impulse?"
        ],
        [ReflectionTrigger.ManualEntry] =
        [
            "Take a moment to check in — how are you feeling about your finances today?",
            "What's one money win you had recently, no matter how small?",
            "What financial habit are you most proud of this week?",
            "Is there a spending pattern you'd like to change?"
        ]
    };

    public ReflectionService(IDbContextFactory<CoinStackDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<Reflection> CreateReflectionAsync(
        ReflectionTrigger trigger,
        int? transactionId = null,
        CancellationToken cancellationToken = default)
    {
        var prompts = Prompts.GetValueOrDefault(trigger, Prompts[ReflectionTrigger.ManualEntry]);
        var prompt = prompts[Random.Shared.Next(prompts.Length)];

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var reflection = new Reflection
        {
            Trigger = trigger,
            Prompt = prompt,
            TransactionId = transactionId,
        };
        db.Reflections.Add(reflection);
        await db.SaveChangesAsync(cancellationToken);
        return reflection;
    }

    public async Task CompleteReflectionAsync(
        int reflectionId,
        string response,
        int moodBefore,
        int moodAfter,
        EmotionTag? emotionTag = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Reflections.FirstOrDefaultAsync(x => x.Id == reflectionId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.Response = response;
        existing.MoodBefore = Math.Clamp(moodBefore, 1, 10);
        existing.MoodAfter = Math.Clamp(moodAfter, 1, 10);
        existing.EmotionTag = emotionTag;
        existing.IsCompleted = true;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Reflection>> GetRecentAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Reflections
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<Reflection?> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Reflections
            .AsNoTracking()
            .Where(x => !x.IsCompleted)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<EmotionSpendPattern>> GetEmotionPatternsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var completed = await db.Reflections
            .AsNoTracking()
            .Include(r => r.Transaction)
            .Where(r => r.IsCompleted && r.EmotionTag.HasValue)
            .ToListAsync(cancellationToken);

        if (completed.Count == 0)
        {
            return [];
        }

        var patterns = completed
            .GroupBy(r => r.EmotionTag!.Value)
            .Select(g =>
            {
                var count = g.Count();
                var totalSpend = g
                    .Where(r => r.Transaction is not null && r.Transaction.Type == TransactionType.Expense)
                    .Sum(r => r.Transaction!.Amount);

                var insight = g.Key switch
                {
                    EmotionTag.Stressed => $"You tend to overspend when stressed ({count}x recorded).",
                    EmotionTag.Tired => $"Tiredness led to {count} spending reflection{(count > 1 ? "s" : "")}.",
                    EmotionTag.Impulsive => $"Impulse buying linked to {count} transaction{(count > 1 ? "s" : "")}.",
                    EmotionTag.Bored => $"Boredom spending appeared {count} time{(count > 1 ? "s" : "")}.",
                    EmotionTag.Anxious => $"Anxiety-driven spending occurred {count} time{(count > 1 ? "s" : "")}.",
                    EmotionTag.Proud => $"You felt proud during {count} financial moment{(count > 1 ? "s" : "")} — keep it up!",
                    EmotionTag.Motivated => $"Motivation drove {count} positive financial decision{(count > 1 ? "s" : "")}.",
                    EmotionTag.Excited => $"Excitement influenced {count} purchase{(count > 1 ? "s" : "")}.",
                    EmotionTag.Guilty => $"Guilt was tagged {count} time{(count > 1 ? "s" : "")} — reflect on patterns.",
                    _ => $"Neutral state recorded {count} time{(count > 1 ? "s" : "")}."
                };

                return new EmotionSpendPattern(g.Key, count, totalSpend, insight);
            })
            .OrderByDescending(p => p.Count)
            .ToList();

        return patterns;
    }
}
