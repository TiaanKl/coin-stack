namespace CoinStack.Data.Entities;

public sealed class CbtJournalEntry : EntityBase
{
    public string Situation { get; set; } = "";

    public string AutomaticThought { get; set; } = "";

    public string Emotion { get; set; } = "";

    public int EmotionIntensity { get; set; }

    public CognitiveDistortion? Distortion { get; set; }

    public string RationalResponse { get; set; } = "";

    public int MoodBefore { get; set; }

    public int MoodAfter { get; set; }

    public decimal? SpendingAmount { get; set; }

    public int? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }
}
