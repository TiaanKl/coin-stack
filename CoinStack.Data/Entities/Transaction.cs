namespace CoinStack.Data.Entities;

public sealed class Transaction : EntityBase
{
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public decimal Amount { get; set; }

    public TransactionType Type { get; set; } = TransactionType.Expense;

    public string Description { get; set; } = "";

    public string? Notes { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public int? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }

    public int? BucketId { get; set; }
    public Bucket? Bucket { get; set; }

    public int? DebtAccountId { get; set; }
    public DebtAccount? DebtAccount { get; set; }

    public ExpenseKind ExpenseKind { get; set; } = ExpenseKind.Discretionary;
    public bool AutoDeduct { get; set; }

    public int? AutoDeductTemplateId { get; set; }

    public bool IsImpulse { get; set; }
}
