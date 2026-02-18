namespace FinanceManager.Data.Entities;

public sealed class Reflection : EntityBase
{
    public ReflectionTrigger Trigger { get; set; }

    public string Prompt { get; set; } = "";

    public string? Response { get; set; }

    public int MoodBefore { get; set; }

    public int MoodAfter { get; set; }

    public bool IsCompleted { get; set; }

    public int? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }
}
