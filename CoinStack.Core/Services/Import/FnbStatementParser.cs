using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace CoinStack.Services.Import;

/// <summary>
/// Layout-aware parser for First National Bank (FNB) statement PDFs
/// ("Transactions in RAND (ZAR)" tabular format).
///
/// Column geometry (A4 portrait, PDF user units):
///   Day + month      : X &lt; 36
///   Description      : 36 .. 302
///   Card/reference   : 302 .. 435  (card number, wrapped continuation text)
///   Amount           : 435 .. 505  (right-aligned at ~472, optional "Cr"/"Dr" suffix)
///   Balance          : 505 .. 548  (right-aligned at ~543, optional "Cr"/"Dr" suffix)
///   Accrued charges  : X &gt;= 548  (ignored)
/// </summary>
public static partial class FnbStatementParser
{
    private const double RowYTolerance = 1.5;

    private const double DateColumnMaxX = 36.0;
    private const double DescriptionMaxX = 302.0;
    private const double MiddleZoneMaxX = 435.0;
    private const double AmountZoneMaxX = 505.0;
    private const double BalanceZoneMaxX = 548.0;

    [GeneratedRegex(@"^\d{1,2}$")]
    private static partial Regex DayNumberRegex();

    [GeneratedRegex(@"^(?<num>\d{1,3}(?:,\d{3})+(?:\.\d{1,2})?|\d+(?:\.\d{1,2})?)(?<sfx>Cr|Dr)?$", RegexOptions.IgnoreCase)]
    private static partial Regex MoneyRegex();

    [GeneratedRegex(@"^\d{6}\*\d{4}$")]
    private static partial Regex CardNumberRegex();

    [GeneratedRegex(@"^\d{5,}$")]
    private static partial Regex NumericReferenceRegex();

    [GeneratedRegex(@"^\d{10,}$")]
    private static partial Regex LongNumberRegex();

    private static readonly string[] MonthNames =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    ];

    public static bool IsFnbStatement(string pageOneText)
    {
        return pageOneText.Contains("Statement Period", StringComparison.OrdinalIgnoreCase)
               && pageOneText.Contains("FNB", StringComparison.OrdinalIgnoreCase)
               && pageOneText.Contains("Transactions in RAND", StringComparison.OrdinalIgnoreCase);
    }

    public static ParsedStatement Parse(Stream pdfStream)
    {
        var statement = new ParsedStatement();

        using var document = PdfDocument.Open(pdfStream);

        var pageRows = new List<List<Row>>(document.NumberOfPages);

        for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
        {
            var page = document.GetPage(pageNumber);
            var rows = GroupIntoRows(page.GetWords());
            pageRows.Add(rows);

            if (pageNumber == 1)
            {
                ParseHeaderFields(rows, statement);
            }

            ParseTransactionTable(rows, statement, pageNumber);
        }

        ResolveTransactionYears(statement);
        FilterNonMovementRows(statement);
        ReconcileBalances(statement);

        if (statement.Transactions.Count > 0)
        {
            statement.Transactions[^1].IsFinalRow = true;
        }

        return statement;
    }

    private sealed class Row
    {
        public List<Word> Words { get; } = [];

        public string Text => string.Join(" ", Words.Select(w => w.Text));
    }

    private static List<Row> GroupIntoRows(IEnumerable<Word> words)
    {
        var rows = new List<Row>();
        Row? current = null;
        var anchorY = double.NaN;

        foreach (var word in words.OrderBy(w => -w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left))
        {
            var y = word.BoundingBox.Bottom;

            if (current is null || Math.Abs(y - anchorY) > RowYTolerance)
            {
                current = new Row();
                rows.Add(current);
                anchorY = y;
            }

            current.Words.Add(word);
        }

        return rows;
    }

    private static void ParseHeaderFields(List<Row> rows, ParsedStatement statement)
    {
        foreach (var row in rows)
        {
            var text = row.Text;

            var periodMatch = Regex.Match(
                text,
                @"Statement\s+Period\s*:\s*(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})\s+to\s+(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})",
                RegexOptions.IgnoreCase);

            if (periodMatch.Success)
            {
                if (TryBuildDate(periodMatch.Groups[1].Value, periodMatch.Groups[2].Value, periodMatch.Groups[3].Value, out var start))
                {
                    statement.PeriodStartUtc = start;
                }

                if (TryBuildDate(periodMatch.Groups[4].Value, periodMatch.Groups[5].Value, periodMatch.Groups[6].Value, out var end))
                {
                    statement.PeriodEndUtc = end;
                }

                continue;
            }

            var dateMatch = Regex.Match(
                text,
                @"Statement\s+Date\s*:\s*(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})",
                RegexOptions.IgnoreCase);

            if (dateMatch.Success)
            {
                if (TryBuildDate(dateMatch.Groups[1].Value, dateMatch.Groups[2].Value, dateMatch.Groups[3].Value, out var statementDate))
                {
                    statement.StatementDateUtc = statementDate;
                }

                continue;
            }

            var accountMatch = Regex.Match(text, @"Account\s*:\s*(\d{6,})", RegexOptions.IgnoreCase);
            if (accountMatch.Success && statement.AccountNumber is null)
            {
                statement.AccountNumber = accountMatch.Groups[1].Value;
                continue;
            }

            var openingMatch = Regex.Match(text, @"Opening\s+Balance\s+(?<amt>" + MoneyPatternText + @")\s*(?<sfx>Cr|Dr)?", RegexOptions.IgnoreCase);
            if (openingMatch.Success)
            {
                statement.OpeningBalance = ParseSignedBalance(openingMatch.Groups["amt"].Value, openingMatch.Groups["sfx"].Value);
                continue;
            }

            var closingMatch = Regex.Match(text, @"Closing\s+Balance\s+(?<amt>" + MoneyPatternText + @")\s*(?<sfx>Cr|Dr)?", RegexOptions.IgnoreCase);
            if (closingMatch.Success)
            {
                statement.ClosingBalance = ParseSignedBalance(closingMatch.Groups["amt"].Value, closingMatch.Groups["sfx"].Value);
            }
        }
    }

    private const string MoneyPatternText = @"\d{1,3}(?:,\d{3})+(?:\.\d{1,2})?|\d+(?:\.\d{1,2})?";

    private static void ParseTransactionTable(List<Row> rows, ParsedStatement statement, int pageNumber)
    {
        var tableActive = false;

        foreach (var row in rows)
        {
            if (!tableActive)
            {
                if (IsTableHeaderRow(row))
                {
                    tableActive = true;
                }

                continue;
            }

            var firstWord = row.Words.FirstOrDefault(w => w.BoundingBox.Left < 60);
            var firstText = firstWord?.Text ?? "";

            if (firstText.Equals("Closing", StringComparison.OrdinalIgnoreCase)
                || firstText.Equals("Turnover", StringComparison.OrdinalIgnoreCase))
            {
                tableActive = false;
                continue;
            }

            var dayWord = row.Words.FirstOrDefault(w =>
                w.BoundingBox.Left < DateColumnMaxX
                && DayNumberRegex().IsMatch(w.Text)
                && int.TryParse(w.Text, out var d)
                && d is >= 1 and <= 31);

            var monthWord = row.Words.FirstOrDefault(w =>
                w.BoundingBox.Left < DateColumnMaxX
                && w.Text.Length >= 3
                && TryParseMonth(w.Text, out _));

            if (dayWord is null || monthWord is null)
            {
                continue;
            }

            if (!int.TryParse(dayWord.Text, out var day) || !TryParseMonth(monthWord.Text, out var month))
            {
                continue;
            }

            var transaction = new ParsedStatementTransaction
            {
                DateUtc = new DateTime(2000, month, Math.Min(day, DateTime.DaysInMonth(2000, month)), 0, 0, 0, DateTimeKind.Utc),
                PageNumber = pageNumber
            };

            var description = new StringBuilder();
            var middleText = new StringBuilder();
            var amountFound = false;

            foreach (var word in row.Words)
            {
                var left = word.BoundingBox.Left;

                if (left < DateColumnMaxX)
                {
                    continue;
                }

                if (left < DescriptionMaxX)
                {
                    if (description.Length > 0)
                    {
                        description.Append(' ');
                    }

                    description.Append(word.Text);
                    continue;
                }

                if (left < MiddleZoneMaxX)
                {
                    if (IsCardOrReferenceWord(word.Text))
                    {
                        continue;
                    }

                    if (middleText.Length > 0)
                    {
                        middleText.Append(' ');
                    }

                    middleText.Append(word.Text);
                    continue;
                }

                if (left < AmountZoneMaxX)
                {
                    var amountMatch = MoneyRegex().Match(word.Text);
                    if (amountMatch.Success && !amountFound)
                    {
                        transaction.Amount = ParseMoney(amountMatch.Groups["num"].Value);
                        transaction.IsCredit = amountMatch.Groups["sfx"].Value.Equals("Cr", StringComparison.OrdinalIgnoreCase);
                        amountFound = true;
                    }
                    else if (!transaction.IsCredit
                             && word.Text.Equals("Cr", StringComparison.OrdinalIgnoreCase))
                    {
                        transaction.IsCredit = true;
                    }

                    continue;
                }

                if (left < BalanceZoneMaxX)
                {
                    var balanceMatch = MoneyRegex().Match(word.Text);
                    if (balanceMatch.Success && transaction.BalanceAfter is null)
                    {
                        transaction.BalanceAfter = ParseSignedBalance(
                            balanceMatch.Groups["num"].Value,
                            balanceMatch.Groups["sfx"].Value);
                    }
                    else if (word.Text.Equals("Dr", StringComparison.OrdinalIgnoreCase) && transaction.BalanceAfter is not null)
                    {
                        transaction.BalanceAfter = -Math.Abs(transaction.BalanceAfter.Value);
                    }
                    else if (word.Text.Equals("Cr", StringComparison.OrdinalIgnoreCase) && transaction.BalanceAfter is not null)
                    {
                        transaction.BalanceAfter = Math.Abs(transaction.BalanceAfter.Value);
                    }
                }
            }

            if (!amountFound)
            {
                statement.Warnings.Add($"Page {pageNumber}: skipped a row without a parseable amount (\"{row.Text}\").");
                continue;
            }

            if (middleText.Length > 0)
            {
                description.Append(' ').Append(middleText);
            }

            transaction.Description = NormalizeWhitespace(description.ToString());

            if (string.IsNullOrWhiteSpace(transaction.Description))
            {
                transaction.Description = transaction.IsCredit
                    ? "Bank Credit (interest / fee reversal)"
                    : "Bank Charges";
            }

            statement.Transactions.Add(transaction);
        }
    }

    private static bool IsTableHeaderRow(Row row)
    {
        var hasDate = row.Words.Any(w => w.Text.Equals("Date", StringComparison.OrdinalIgnoreCase) && w.BoundingBox.Left < 60);
        var hasDescription = row.Words.Any(w => w.Text.StartsWith("Description", StringComparison.OrdinalIgnoreCase));
        var hasAmount = row.Words.Any(w => w.Text.Equals("Amount", StringComparison.OrdinalIgnoreCase));
        var hasBalance = row.Words.Any(w => w.Text.StartsWith("Balance", StringComparison.OrdinalIgnoreCase));

        return hasDate && hasDescription && hasAmount && hasBalance;
    }

    private static bool IsCardOrReferenceWord(string text)
    {
        if (CardNumberRegex().IsMatch(text))
        {
            return true;
        }

        if (NumericReferenceRegex().IsMatch(text))
        {
            return true;
        }

        if (DayNumberRegex().IsMatch(text))
        {
            return true;
        }

        return TryParseMonth(text, out _);
    }

    private static bool TryParseMonth(string text, out int month)
    {
        month = 0;

        if (text.Length < 3)
        {
            return false;
        }

        for (var i = 0; i < MonthNames.Length; i++)
        {
            if (MonthNames[i].StartsWith(text[..3], StringComparison.OrdinalIgnoreCase))
            {
                month = i + 1;
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildDate(string dayText, string monthText, string yearText, out DateTime date)
    {
        date = default;

        return int.TryParse(dayText, out var day)
               && int.TryParse(yearText, out var year)
               && TryParseMonth(monthText, out var month)
               && TryMakeDate(day, month, year, out date);
    }

    private static bool TryMakeDate(int day, int month, int year, out DateTime date)
    {
        if (year is < 1990 or > 2100 || month is < 1 or > 12)
        {
            date = default;
            return false;
        }

        day = Math.Min(day, DateTime.DaysInMonth(year, month));
        date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        return true;
    }

    private static decimal ParseMoney(string text)
    {
        return decimal.Parse(text.Replace(",", ""), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// FNB balance convention: an explicit "Cr" suffix means a credit (positive) balance;
    /// a missing suffix or an explicit "Dr" suffix means an overdrawn (negative) balance.
    /// </summary>
    private static decimal ParseSignedBalance(string amountText, string suffix)
    {
        var value = ParseMoney(amountText);
        return suffix.Equals("Cr", StringComparison.OrdinalIgnoreCase) ? value : -value;
    }

    private static void ResolveTransactionYears(ParsedStatement statement)
    {
        if (statement.Transactions.Count == 0)
        {
            return;
        }

        var start = statement.PeriodStartUtc;
        var end = statement.PeriodEndUtc ?? statement.StatementDateUtc;

        foreach (var transaction in statement.Transactions)
        {
            var month = transaction.DateUtc.Month;
            var day = transaction.DateUtc.Day;
            int year;

            if (start is not null && end is not null && start.Value.Year != end.Value.Year)
            {
                year = month >= start.Value.Month ? start.Value.Year : end.Value.Year;
            }
            else if (start is not null)
            {
                year = start.Value.Year;
            }
            else if (statement.StatementDateUtc is not null)
            {
                year = statement.StatementDateUtc.Value.Year;
                if (month > statement.StatementDateUtc.Value.Month && month - statement.StatementDateUtc.Value.Month > 6)
                {
                    year--;
                }
            }
            else
            {
                year = DateTime.UtcNow.Year;
            }

            if (TryMakeDate(day, month, year, out var resolved))
            {
                transaction.DateUtc = resolved;
            }
        }

        for (var i = 1; i < statement.Transactions.Count; i++)
        {
            var previous = statement.Transactions[i - 1].DateUtc;
            var current = statement.Transactions[i];

            if (current.DateUtc < previous)
            {
                var bumped = current.DateUtc.AddYears(1);
                if (end is null || bumped <= end.Value.AddDays(31))
                {
                    current.DateUtc = bumped;
                }
            }
        }
    }

    private static void FilterNonMovementRows(ParsedStatement statement)
    {
        // A row whose printed balance equals the previous row's balance moved no money
        // (e.g. failed collection attempts or zero-value scheduled transfers that FNB
        // still lists with an amount). Those would double-count if imported.
        var kept = new List<ParsedStatementTransaction>(statement.Transactions.Count);

        foreach (var transaction in statement.Transactions)
        {
            var previous = kept.LastOrDefault();
            var previousBalance = previous?.BalanceAfter ?? statement.OpeningBalance;

            if (previousBalance is not null
                && transaction.BalanceAfter is not null
                && Math.Abs(transaction.BalanceAfter.Value - previousBalance.Value) < 0.005m)
            {
                statement.InformationalRowsSkipped++;
                continue;
            }

            kept.Add(transaction);
        }

        statement.Transactions.Clear();
        statement.Transactions.AddRange(kept);
    }

    private static void ReconcileBalances(ParsedStatement statement)
    {
        if (statement.Transactions.Count == 0 || statement.OpeningBalance is null)
        {
            return;
        }

        var running = statement.OpeningBalance.Value;
        var mismatches = 0;
        var verified = 0;

        foreach (var transaction in statement.Transactions)
        {
            running += transaction.IsCredit ? transaction.Amount : -transaction.Amount;

            if (transaction.BalanceAfter is null)
            {
                continue;
            }

            verified++;
            if (Math.Abs(running - transaction.BalanceAfter.Value) > 0.02m)
            {
                mismatches++;
            }
        }

        if (verified > 0 && mismatches > verified / 4)
        {
            statement.Warnings.Add(
                $"Balance reconciliation mismatched on {mismatches} of {verified} rows; " +
                "amounts were kept as printed but double-check the imported totals.");
        }
    }

    private static string NormalizeWhitespace(string text)
    {
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}
