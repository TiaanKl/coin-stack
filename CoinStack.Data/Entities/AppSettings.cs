namespace CoinStack.Data.Entities;

public sealed class AppSettings : EntityBase
{
    /// <summary>The currency for new installs and for any code path that has not loaded settings yet.</summary>
    public const string DefaultCurrency = "ZAR";

    public string Currency { get; set; } = DefaultCurrency;
    public int MonthStartDay { get; set; } = 1;
    public decimal MonthlyIncome { get; set; }

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

    public bool ShowReserveAwareBudget { get; set; } = false;

    public bool EnableEmergencyFallback { get; set; } = true;

    /// <summary>
    /// When the daily catch-up last ran. The next run fills in every pay period since then, so a period
    /// the app was never opened in still gets its auto-deductions. Not a user setting.
    /// </summary>
    public DateTime? LastCatchUpUtc { get; set; }
}
