namespace CoinStack.Data.Entities;

public sealed class AppSettings : EntityBase
{
    public string Currency { get; set; } = "USD";
    public int MonthStartDay { get; set; } = 1;
    public decimal MonthlyIncome { get; set; } = 5000;
    public bool EnableScoring { get; set; } = true;
    public bool EnableStreaks { get; set; } = true;
    public bool EnableToast { get; set; } = true;
    public bool EnableSounds { get; set; } = true;
    public bool EnableReflections { get; set; } = true;
    public int LargeExpenseThreshold { get; set; } = 50;

    public bool SavingsIsPercent { get; set; } = false;
    public decimal MonthlySavingsAmount { get; set; } = 0;
    public decimal MonthlySavingsPercent { get; set; } = 0;
    public decimal? SavingsInterestRate { get; set; }
    public bool SavingsInterestIsYearly { get; set; } = true;
}
