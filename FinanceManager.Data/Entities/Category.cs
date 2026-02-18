namespace FinanceManager.Data.Entities;

public sealed class Category : EntityBase
{
    public string Name { get; set; } = "";

    /// <summary>
    /// Optional hex color (e.g. #RRGGBB).
    /// </summary>
    public string? ColorHex { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
}
