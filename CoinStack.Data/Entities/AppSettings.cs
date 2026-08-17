namespace CoinStack.Data.Entities;

public sealed class AppSettings : EntityBase
{
    public string Currency { get; set; } = "USD";
    public int MonthStartDay { get; set; } = 1;
    public decimal MonthlyIncome { get; set; }

    /// <summary>
    /// User-reported bank/cash balance snapshot. May be negative (overdrawn).
    /// Combined with transactions after <see cref="BankBalanceAsOfUtc"/> for the live effective balance.
    /// </summary>
    public decimal CurrentBankBalance { get; set; }

    /// <summary>
    /// When <see cref="CurrentBankBalance"/> was last set by the user. Transactions after this
    /// timestamp adjust the effective balance without double-counting history.
    /// </summary>
    public DateTime? BankBalanceAsOfUtc { get; set; }

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
}
