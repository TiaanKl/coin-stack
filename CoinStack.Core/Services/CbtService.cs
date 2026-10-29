using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class CbtService : ICbtService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    public CbtService(IDbContextFactory<CoinStackDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<List<CbtJournalEntry>> GetAllEntriesAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.CbtJournalEntries
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<CbtJournalEntry?> GetEntryByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.CbtJournalEntries.FindAsync([id], ct);
    }

    public async Task<CbtJournalEntry> CreateEntryAsync(CbtJournalEntry entry, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        db.CbtJournalEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task UpdateEntryAsync(CbtJournalEntry entry, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        db.CbtJournalEntries.Update(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteEntryAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entry = await db.CbtJournalEntries.FindAsync([id], ct);
        if (entry is not null)
        {
            db.CbtJournalEntries.Remove(entry);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<List<CbtJournalEntry>> GetRecentEntriesAsync(int count, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.CbtJournalEntries
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<Dictionary<CognitiveDistortion, int>> GetDistortionFrequencyAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.CbtJournalEntries
            .AsNoTracking()
            .Where(e => e.Distortion != null)
            .GroupBy(e => e.Distortion!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
    }

    public string GetDistortionLabel(CognitiveDistortion distortion) => distortion switch
    {
        CognitiveDistortion.AllOrNothing => "All-or-Nothing Thinking",
        CognitiveDistortion.Catastrophizing => "Catastrophizing",
        CognitiveDistortion.EmotionalReasoning => "Emotional Reasoning",
        CognitiveDistortion.FortuneTelling => "Fortune Telling",
        CognitiveDistortion.MentalFilter => "Mental Filter",
        CognitiveDistortion.Overgeneralisation => "Overgeneralisation",
        CognitiveDistortion.Personalisation => "Personalisation",
        CognitiveDistortion.ShouldStatements => "Should Statements",
        CognitiveDistortion.Labelling => "Labelling",
        CognitiveDistortion.MagnificationMinimisation => "Magnification / Minimisation",
        CognitiveDistortion.ScarcityThinking => "Scarcity Thinking",
        CognitiveDistortion.SocialComparison => "Social Comparison / FOMO",
        _ => distortion.ToString()
    };

    public string GetDistortionDescription(CognitiveDistortion distortion) => distortion switch
    {
        CognitiveDistortion.AllOrNothing => "Seeing your finances in black-and-white terms. \"If I can't save £500 a month, there's no point saving at all.\"",
        CognitiveDistortion.Catastrophizing => "Expecting the worst financial outcome. \"One unexpected bill means I'll be bankrupt.\"",
        CognitiveDistortion.EmotionalReasoning => "Assuming your feelings reflect reality. \"I feel broke, so I must be broke.\"",
        CognitiveDistortion.FortuneTelling => "Predicting negative financial futures without evidence. \"I'll never be able to afford a house.\"",
        CognitiveDistortion.MentalFilter => "Focusing only on financial negatives while ignoring positives. Dwelling on one overspend while ignoring months of saving.",
        CognitiveDistortion.Overgeneralisation => "Drawing sweeping conclusions from one event. \"I overspent once, so I'm terrible with money.\"",
        CognitiveDistortion.Personalisation => "Blaming yourself for financial situations beyond your control. \"Prices went up because I'm unlucky.\"",
        CognitiveDistortion.ShouldStatements => "Rigid rules about money. \"I should never spend on fun things.\" This creates guilt and shame.",
        CognitiveDistortion.Labelling => "Attaching a fixed label to yourself. \"I'm a spender\" instead of \"I overspent on one occasion.\"",
        CognitiveDistortion.MagnificationMinimisation => "Exaggerating problems or minimising achievements. Making a small debt feel enormous while ignoring savings progress.",
        CognitiveDistortion.ScarcityThinking => "Constant fear of not having enough, leading to hoarding or panic spending.",
        CognitiveDistortion.SocialComparison => "Measuring your financial worth against others, especially on social media. \"Everyone else can afford holidays.\"",
        _ => ""
    };

    public string GetReframingPrompt(CognitiveDistortion distortion) => distortion switch
    {
        CognitiveDistortion.AllOrNothing => "What small step could you take today? Even saving a tiny amount counts.",
        CognitiveDistortion.Catastrophizing => "What's the most likely outcome, not the worst? How have you handled similar situations before?",
        CognitiveDistortion.EmotionalReasoning => "What do the actual numbers show? Separate how you feel from the facts.",
        CognitiveDistortion.FortuneTelling => "What evidence supports this prediction? What evidence contradicts it?",
        CognitiveDistortion.MentalFilter => "List three positive financial things you've done recently, no matter how small.",
        CognitiveDistortion.Overgeneralisation => "Is one event really a pattern? Think of times you made good financial choices.",
        CognitiveDistortion.Personalisation => "What factors were outside your control? How would you view this if it happened to a friend?",
        CognitiveDistortion.ShouldStatements => "Replace 'should' with 'I'd like to'. What would a balanced approach look like?",
        CognitiveDistortion.Labelling => "Describe the behaviour, not the person. What would you say to a friend in this situation?",
        CognitiveDistortion.MagnificationMinimisation => "Rate this situation on a scale of 1-10 in real impact. Now compare with your emotional reaction.",
        CognitiveDistortion.ScarcityThinking => "Look at your actual financial position. What resources do you have right now?",
        CognitiveDistortion.SocialComparison => "You're seeing someone else's highlight reel. What are your own financial wins?",
        _ => "What would a compassionate friend say about this situation?"
    };

    public IReadOnlyList<CbtExercise> GetExercises() =>
    [
        new("thought-record", "Thought Record", "Identify and challenge unhelpful financial thoughts using the CBT thought record technique.",
            "fa-brain", CbtExerciseType.ThoughtRecord,
            ["Describe the situation that triggered the thought",
             "Write down the automatic thought exactly as it occurred",
             "Rate the emotion intensity (1-10)",
             "Identify the cognitive distortion",
             "Write a balanced, rational alternative",
             "Re-rate your emotion intensity"]),

        new("spending-pause", "Mindful Spending Pause", "Before any non-essential purchase, pause and work through this exercise.",
            "fa-pause-circle", CbtExerciseType.MindfulSpending,
            ["Notice the urge to buy — what triggered it?",
             "What emotion are you feeling right now?",
             "Is this a need or a want?",
             "Will this matter in a week? A month?",
             "What would Future You think about this purchase?",
             "If you still want it, add it to the Waitlist with a cool-off period"]),

        new("values-check", "Values Alignment Check", "Ensure your spending aligns with what truly matters to you.",
            "fa-compass", CbtExerciseType.ValuesClarification,
            ["List your top 5 values (e.g., security, family, health, freedom, growth)",
             "Look at your last 10 transactions",
             "For each, ask: does this align with my values?",
             "Identify any misalignments",
             "Set one intention for your next purchase"]),

        new("gratitude-audit", "Financial Gratitude Audit", "Shift from scarcity thinking to abundance by recognising what you have.",
            "fa-heart", CbtExerciseType.GratitudeJournal,
            ["List 3 financial things you're grateful for (even small ones)",
             "Note one bill you're glad you can pay",
             "Acknowledge one good money decision you made recently",
             "Describe how your financial situation has improved, even slightly",
             "Set one positive intention for tomorrow"]),

        new("future-self", "Letter to Future You", "Connect with your future self to strengthen long-term motivation.",
            "fa-envelope", CbtExerciseType.BehavioralExperiment,
            ["Imagine yourself 5 years from now",
             "What financial position do you want to be in?",
             "Write a short letter from Future You, thanking Present You for the steps you're taking",
             "Identify one action you can take today that Future You would appreciate",
             "Commit to that action now"]),

        new("trigger-map", "Spending Trigger Map", "Understand what drives your spending impulses.",
            "fa-map", CbtExerciseType.ExposureExercise,
            ["Think of your last impulse purchase",
             "What happened just before? (event, emotion, time of day)",
             "What were you feeling? (stressed, bored, excited, tired)",
             "What did you tell yourself to justify it?",
             "What could you do differently next time you feel that way?",
             "Create an if-then plan: 'If I feel [trigger], then I will [alternative]'"]),
    ];
}
