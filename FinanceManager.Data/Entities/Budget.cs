namespace FinanceManager.Data.Entities;

public sealed class Budget : EntityBase
{
    public int Year { get; set; }
    public int Month { get; set; }

    public decimal LimitAmount { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
}
