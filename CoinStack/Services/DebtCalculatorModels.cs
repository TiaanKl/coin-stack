namespace CoinStack.Services;

public enum DebtInterestType
{
    Simple = 0,
    Compound = 1,
    Amortizing = 2,
    None = 3
}

public enum DebtCompoundingFrequency
{
    Monthly = 0,
    Annual = 1
}

public sealed class DebtCalculationInput
{
    public decimal? Principal { get; set; }
    public decimal? InterestRate { get; set; }
    public DebtInterestType? InterestType { get; set; }
    public DebtCompoundingFrequency? CompoundingFrequency { get; set; }
    public decimal? MonthlyPayment { get; set; }
    public decimal? TotalOwed { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? TermMonths { get; set; }
    public int? PaymentsMade { get; set; }
}

public sealed class DebtCalculationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public List<string> Inferences { get; init; } = [];

    public DebtInterestType ResolvedInterestType { get; init; }
    public DebtCompoundingFrequency? CompoundingFrequency { get; init; }

    public decimal? Principal { get; init; }
    public decimal? InterestRate { get; init; }
    public decimal? MonthlyPayment { get; init; }
    public decimal? TotalOwed { get; init; }
    public decimal? InterestAmount { get; init; }
    public decimal? RemainingBalanceAfterPayments { get; init; }

    public int? TermMonths { get; init; }
    public decimal? Years { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }

    public string FormulaUsed { get; init; } = "";
}
