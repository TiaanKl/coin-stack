namespace CoinStack.Data.Entities;

public sealed class Category : EntityBase
{
    public string Name { get; set; } = "";

    public string? ColorHex { get; set; }

    public CategoryScope Scope { get; set; } = CategoryScope.Both;

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
    public ICollection<Bucket> Buckets { get; set; } = new List<Bucket>();
}
