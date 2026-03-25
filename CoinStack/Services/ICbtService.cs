using CoinStack.Data.Entities;

namespace CoinStack.Services;

public interface ICbtService
{
    Task<List<CbtJournalEntry>> GetAllEntriesAsync(CancellationToken ct = default);
    Task<CbtJournalEntry?> GetEntryByIdAsync(int id, CancellationToken ct = default);
    Task<CbtJournalEntry> CreateEntryAsync(CbtJournalEntry entry, CancellationToken ct = default);
    Task UpdateEntryAsync(CbtJournalEntry entry, CancellationToken ct = default);
    Task DeleteEntryAsync(int id, CancellationToken ct = default);
    Task<List<CbtJournalEntry>> GetRecentEntriesAsync(int count, CancellationToken ct = default);
    Task<Dictionary<CognitiveDistortion, int>> GetDistortionFrequencyAsync(CancellationToken ct = default);
    string GetDistortionLabel(CognitiveDistortion distortion);
    string GetDistortionDescription(CognitiveDistortion distortion);
    string GetReframingPrompt(CognitiveDistortion distortion);
    IReadOnlyList<CbtExercise> GetExercises();
}

public sealed record CbtExercise(
    string Id,
    string Title,
    string Description,
    string Icon,
    CbtExerciseType Type,
    IReadOnlyList<string> Steps);

public enum CbtExerciseType
{
    ThoughtRecord,
    BehavioralExperiment,
    ExposureExercise,
    MindfulSpending,
    ValuesClarification,
    GratitudeJournal
}
