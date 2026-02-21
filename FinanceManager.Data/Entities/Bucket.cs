namespace CoinStack.Data.Entities;

public sealed class Bucket : EntityBase
{
    public string Name { get; set; } = "";

    public decimal AllocatedAmount { get; set; }

    public string? ColorHex { get; set; }

    public string? Icon { get; set; }

    public bool IsDefault { get; set; }

    public int SortOrder { get; set; }

    public bool IsSavings { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
}
