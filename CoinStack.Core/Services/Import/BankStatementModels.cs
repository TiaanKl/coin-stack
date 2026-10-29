namespace CoinStack.Services.Import;

/// <summary>One transaction row extracted from a bank statement PDF.</summary>
public sealed class ParsedStatementTransaction
{
    public DateTime DateUtc { get; set; }

    /// <summary>Absolute amount in the statement currency (always positive).</summary>
    public decimal Amount { get; set; }

    /// <summary>True when the statement marks the amount as a credit (Cr).</summary>
    public bool IsCredit { get; set; }

    public string Description { get; set; } = "";

    public string? RawReference { get; set; }

    public decimal? BalanceAfter { get; set; }

    public int PageNumber { get; set; }

    /// <summary>Running balance at the end of the statement (from the last row).</summary>
    public bool IsFinalRow { get; set; }
}

/// <summary>Everything extracted from one uploaded statement PDF.</summary>
public sealed class ParsedStatement
{
    public string BankFormat { get; set; } = "FNB";

    public string? AccountNumber { get; set; }

    public DateTime? PeriodStartUtc { get; set; }
    public DateTime? PeriodEndUtc { get; set; }
    public DateTime? StatementDateUtc { get; set; }

    /// <summary>Negative when the account was overdrawn (Dr balance).</summary>
    public decimal? OpeningBalance { get; set; }
    public decimal? ClosingBalance { get; set; }

    public List<ParsedStatementTransaction> Transactions { get; } = [];

    public List<string> Warnings { get; } = [];

    /// <summary>Rows printed with no balance movement (failed collection attempts, zero-value scheduled payments).</summary>
    public int InformationalRowsSkipped { get; set; }
}

/// <summary>Summary of spending with a merchant for a specific calendar month.</summary>
public sealed record MerchantMonthlySpend(
    string MonthLabel,
    DateTime MonthDate,
    decimal TotalAmount,
    int TransactionCount);

/// <summary>Grouped collection of transactions from the same service/merchant across the statement period.</summary>
public sealed class GroupedMerchantCandidate
{
    public string MerchantName { get; set; } = "";
    public string NormalizedKey { get; set; } = "";
    public string? CategoryName { get; set; }
    public string IconClass { get; set; } = "fa-solid fa-store";
    public CoinStack.Data.Entities.TransactionType Type { get; set; } = CoinStack.Data.Entities.TransactionType.Expense;
    public decimal TotalAmount { get; set; }
    public int TransactionCount => Items.Count;
    public bool IsAvoidableFee { get; set; }
    public string? FeeType { get; set; }
    public bool IsPassThrough { get; set; }

    public List<MerchantMonthlySpend> MonthlyBreakdowns { get; } = [];
    public List<ImportCandidate> Items { get; } = [];

    public bool AllIncluded => Items.Count > 0 && Items.All(i => i.Include || i.AlreadyExists);
    public bool SomeIncluded => Items.Any(i => i.Include && !i.AlreadyExists);
    public int IncludedCount => Items.Count(i => i.Include && !i.AlreadyExists);
    public decimal IncludedAmount => Items.Where(i => i.Include && !i.AlreadyExists).Sum(i => i.Amount);
}
