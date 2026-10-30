namespace CoinStack.Data.Entities;

public sealed class Transaction : EntityBase
{
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public decimal Amount { get; set; }

    public TransactionType Type { get; set; } = TransactionType.Expense;

    /// <summary>The account the money left or reached. For a transfer, the account it left.</summary>
    public int? AccountId { get; set; }
    public Account? Account { get; set; }

    /// <summary>For a transfer, the account the money reached. Null on older transfers, which move nothing.</summary>
    public int? TransferAccountId { get; set; }
    public Account? TransferAccount { get; set; }


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

    public string Source { get; set; } = "Manual";

    public string? ImportFingerprint { get; set; }

    public int? StatementImportId { get; set; }
    public StatementImport? StatementImport { get; set; }
}
