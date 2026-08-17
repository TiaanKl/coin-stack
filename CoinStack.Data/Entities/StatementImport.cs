namespace CoinStack.Data.Entities;

/// <summary>
/// Tracks one uploaded bank statement file and the transactions imported from it,
/// so an import can be inspected or rolled back as a single unit.
/// </summary>
public sealed class StatementImport : EntityBase
{
    public string FileName { get; set; } = "";

    public string BankFormat { get; set; } = "";

    public string? AccountNumber { get; set; }

    public DateTime? PeriodStartUtc { get; set; }
    public DateTime? PeriodEndUtc { get; set; }

    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }

    public int RowsParsed { get; set; }
    public int TransactionsImported { get; set; }
    public int DuplicatesSkipped { get; set; }
    public int TransfersSkipped { get; set; }

    public StatementImportStatus Status { get; set; } = StatementImportStatus.Parsed;
}
