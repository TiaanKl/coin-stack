namespace CoinStack.Services.Import;

/// <summary>
/// Reads a bank statement file into a parsed statement. FNB PDF is the supported feed.
/// A live aggregator (e.g. Stitch) can implement this later without rewriting import commit.
/// </summary>
public interface IBankFeed
{
    string FormatId { get; }

    bool CanParse(string fileName);

    ParsedStatement Parse(Stream stream, string fileName);
}

/// <summary>First National Bank statement PDFs ("Transactions in RAND").</summary>
public sealed class FnbPdfBankFeed : IBankFeed
{
    public string FormatId => "FNB";

    public bool CanParse(string fileName)
    {
        return fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public ParsedStatement Parse(Stream stream, string fileName)
    {
        return FnbStatementParser.Parse(stream);
    }
}

/// <summary>FNB transaction-history CSV / OFX — current activity, not the lagged monthly PDF.</summary>
public sealed class FnbHistoryBankFeed : IBankFeed
{
    public string FormatId => "FNB-HISTORY";

    public bool CanParse(string fileName)
    {
        return fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".ofx", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".qfx", StringComparison.OrdinalIgnoreCase);
    }

    public ParsedStatement Parse(Stream stream, string fileName)
    {
        return FnbTransactionHistoryParser.Parse(stream, fileName);
    }
}

/// <summary>Routes an upload to the PDF statement parser or the transaction-history parser.</summary>
public sealed class CompositeBankFeed : IBankFeed
{
    private readonly IReadOnlyList<IBankFeed> _feeds;

    public CompositeBankFeed(IEnumerable<IBankFeed> feeds)
    {
        _feeds = feeds.Where(f => f is not CompositeBankFeed).ToList();
    }

    public string FormatId => "FNB";

    public bool CanParse(string fileName) => _feeds.Any(f => f.CanParse(fileName));

    public ParsedStatement Parse(Stream stream, string fileName)
    {
        var feed = _feeds.FirstOrDefault(f => f.CanParse(fileName))
                   ?? throw new InvalidOperationException($"No bank feed accepts \"{fileName}\".");
        return feed.Parse(stream, fileName);
    }
}
