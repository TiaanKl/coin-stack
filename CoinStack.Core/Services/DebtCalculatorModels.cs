namespace CoinStack.Services;

public enum DebtInterestType
{
    /// <summary>Flat ("add-on") interest: principal × rate × years, repaid in equal instalments.</summary>
    Simple = 0,

    /// <summary>Interest compounds on the outstanding balance at <see cref="DebtCompoundingFrequency"/>.</summary>
    Compound = 1,

    /// <summary>A standard instalment loan: interest on the outstanding balance each month.</summary>
    Amortizing = 2,

    None = 3
}

public enum DebtCompoundingFrequency
{
    Monthly = 0,
    Annual = 1,

    /// <summary>Daily interest on the balance (credit cards and APR-quoted loans).</summary>
    Daily = 2
}

public sealed class DebtCalculationInput
{
    public decimal? Principal { get; set; }

    /// <summary>Annual interest rate as a percentage: 11.75 means 11.75% a year.</summary>
    public decimal? AnnualRatePercent { get; set; }

    public DebtInterestType? InterestType { get; set; }
    public DebtCompoundingFrequency? CompoundingFrequency { get; set; }
    public decimal? MonthlyPayment { get; set; }
    public decimal? TotalOwed { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? FirstPaymentDate { get; set; }
    public int? TermMonths { get; set; }
    public int? PaymentsMade { get; set; }

    /// <summary>When set, the result also shows the instalment if the rate rises (variable-rate loans).</summary>
    public bool IsVariableRate { get; set; }
}

/// <summary>What the loan would cost if a variable rate moved by <see cref="RateChangePercentPoints"/>.</summary>
public sealed record DebtRateScenario(
    decimal RateChangePercentPoints,
    decimal AnnualRate,
    decimal MonthlyPayment,
    decimal TotalOwed,
    decimal InterestAmount);

public sealed class DebtCalculationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public List<string> Inferences { get; init; } = [];

    public DebtInterestType ResolvedInterestType { get; init; }
    public DebtCompoundingFrequency? CompoundingFrequency { get; init; }

    public decimal? Principal { get; init; }

    /// <summary>The annual rate as a fraction (0.1175 = 11.75%).</summary>
    public decimal? InterestRate { get; init; }

    /// <summary>The rate actually charged per month after compounding, as a fraction.</summary>
    public decimal? EffectiveMonthlyRate { get; init; }

    public decimal? MonthlyPayment { get; init; }

    /// <summary>Everything that will be repaid over the life of the debt: principal plus interest.</summary>
    public decimal? TotalOwed { get; init; }

    public decimal? InterestAmount { get; init; }
    public decimal? RemainingBalanceAfterPayments { get; init; }

    public int? TermMonths { get; init; }
    public decimal? Years { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }

    public string FormulaUsed { get; init; } = "";
    public AmortisationScheduleResult? AmortisationSchedule { get; init; }
    public List<DebtRateScenario> RateScenarios { get; init; } = [];
}

public sealed class AmortisationRow
{
    public int PaymentNumber { get; init; }
    public DateTime PaymentDate { get; init; }
    public decimal StartingBalance { get; init; }
    public decimal Payment { get; init; }
    public decimal InterestPortion { get; init; }
    public decimal PrincipalPortion { get; init; }
    public decimal EndingBalance { get; init; }
    public bool IsPaid { get; init; }
}

public sealed class AmortisationScheduleResult
{
    public List<AmortisationRow> Rows { get; init; } = [];
    public decimal TotalInterestPaidToDate { get; init; }
    public decimal TotalPrincipalRepaidToDate { get; init; }
    public decimal TotalAmountPaidToDate { get; init; }
    public decimal CurrentRemainingBalance { get; init; }
    public int PaymentsMadeCount { get; init; }
    public int PaymentsRemainingCount { get; init; }
    public DateTime? PayoffDate { get; init; }
    public decimal PercentageCompleted { get; init; }
}
