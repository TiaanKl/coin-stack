namespace CoinStack.Data.Entities;

public sealed class Bucket : EntityBase
{
    public string Name { get; set; } = "";

    public decimal AllocatedAmount { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public string? Icon { get; set; }

    public bool IsDefault { get; set; }

    public int SortOrder { get; set; }

    public bool IsSavings { get; set; }

    /// <summary>Explicit link to a savings Goal this bucket contributes towards.</summary>
    public int? GoalId { get; set; }
    public Goal? Goal { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
}
