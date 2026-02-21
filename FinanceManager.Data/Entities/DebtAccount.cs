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
}