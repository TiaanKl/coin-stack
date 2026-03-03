namespace CoinStack.Data.Entities;

public sealed class Category : EntityBase
{
    public string Name { get; set; } = "";

    public string? ColorHex { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
}
