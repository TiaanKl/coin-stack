namespace CoinStack.Data.Entities;

/// <summary>
/// Somewhere the person keeps money: a cheque account, savings, the emergency fund or a credit card.
/// Its balance is <see cref="Balance"/> as of <see cref="BalanceAsOfUtc"/>, moved by every transaction
/// on the account after that moment.
/// </summary>
public sealed class Account : EntityBase
{
    public string Name { get; set; } = "";

    public AccountType Type { get; set; } = AccountType.Cheque;

    /// <summary>The bank's account number, used to send an imported statement to the right account.</summary>
    public string? AccountNumber { get; set; }

    /// <summary>The balance at <see cref="BalanceAsOfUtc"/>. Negative when overdrawn or, for a card, owed.</summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// When <see cref="Balance"/> was true. Transactions after it move the balance. Null only on the main
    /// account before the person has entered a balance; then the balance is <see cref="Balance"/> alone.
    /// </summary>
    public DateTime? BalanceAsOfUtc { get; set; }

    /// <summary>For a credit card, the debt it is. The debt's balance follows the card.</summary>
    public int? DebtAccountId { get; set; }
    public DebtAccount? DebtAccount { get; set; }

    /// <summary>The account money comes from and goes to when nothing else is chosen.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Hidden and left out of totals. Accounts with transactions are archived, never deleted.</summary>
    public bool IsArchived { get; set; }

    public int SortOrder { get; set; }
}
