namespace CoinStack.Data.Entities;

public sealed class DebtAccount : EntityBase
{
    public string Name { get; set; } = "";
    public string? Provider { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal CurrentBalance { get; set; }

    public decimal MonthlyPaymentAmount { get; set; }
    public decimal InterestRatePercent { get; set; }

    public DateTime PaymentStartDateUtc { get; set; } = DateTime.UtcNow.Date;
    public int? PlannedTermMonths { get; set; }

    /// <summary>
    /// The last date monthly interest was charged onto <see cref="CurrentBalance"/>.
    /// Null until the first accrual run, which starts the clock without back-charging.
    /// </summary>
    public DateTime? InterestAccruedThroughUtc { get; set; }
}