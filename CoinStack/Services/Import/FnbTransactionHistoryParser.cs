using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CoinStack.Services.Import;

/// <summary>
/// Parses FNB <em>transaction history</em> downloads (CSV / OFX), which are current,
/// unlike monthly PDF statements that often lag by a month or more.
/// </summary>
public static partial class FnbTransactionHistoryParser
{
    private static readonly string[] DateHeaders = ["date", "transaction date", "posted date", "posting date", "waarde", "txn date"];
    private static readonly string[] DescriptionHeaders = ["description", "details", "narrative", "transaction description", "memo"];
    private static readonly string[] AmountHeaders = ["amount", "transaction amount"];
    private static readonly string[] DebitHeaders = ["debit", "dr", "withdrawal"];
    private static readonly string[] CreditHeaders = ["credit", "cr", "deposit"];
    private static readonly string[] BalanceHeaders = ["balance", "running balance", "available balance"];
    private static readonly string[] AccountHeaders = ["account", "account number", "account no"];

    [GeneratedRegex(@"<STMTTRN>(.*?)</STMTTRN>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex OfxTransactionBlockRegex();

    [GeneratedRegex(@"<(DTPOSTED|TRNAMT|MEMO|NAME|TRNTYPE)>([^<\r\n]+)", RegexOptions.IgnoreCase)]
    private static partial Regex OfxFieldRegex();

    public static ParsedStatement Parse(Stream stream, string fileName)
    {
        if (fileName.EndsWith(".ofx", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".qfx", StringComparison.OrdinalIgnoreCase))
        {
            return ParseOfx(ReadText(stream));
        }

        return ParseCsv(ReadText(stream));
    }

    public static ParsedStatement ParseCsv(string text)
    {
        var statement = new ParsedStatement { BankFormat = "FNB-CSV" };
        var lines = SplitLines(text);
        if (lines.Count == 0)
        {
            statement.Warnings.Add("The CSV file was empty.");
            return statement;
        }

        var headerIndex = FindHeaderRow(lines);
        if (headerIndex < 0)
        {
            statement.Warnings.Add("Could not find a header row with Date and Amount (or Debit/Credit) columns.");
            return statement;
        }

        var header = ParseCsvLine(lines[headerIndex]);
        var map = MapColumns(header);

        if (map.DateIndex < 0 || (map.AmountIndex < 0 && map.DebitIndex < 0 && map.CreditIndex < 0) || map.DescriptionIndex < 0)
        {
            statement.Warnings.Add("CSV headers must include a date, a description, and an amount (or debit/credit).");
            return statement;
        }

        for (var i = headerIndex + 1; i < lines.Count; i++)
        {
            var cols = ParseCsvLine(lines[i]);
            if (cols.Count == 0 || cols.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var dateText = Get(cols, map.DateIndex);
            if (!TryParseDate(dateText, out var date))
            {
                continue;
            }

            var description = Get(cols, map.DescriptionIndex).Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            decimal signed;
            if (map.AmountIndex >= 0)
            {
                if (!TryParseMoney(Get(cols, map.AmountIndex), out signed, out _))
                {
                    statement.InformationalRowsSkipped++;
                    continue;
                }
            }
            else
            {
                TryParseMoney(Get(cols, map.DebitIndex), out var debit, out _);
                TryParseMoney(Get(cols, map.CreditIndex), out var credit, out _);
                if (debit == 0 && credit == 0)
                {
                    statement.InformationalRowsSkipped++;
                    continue;
                }

                signed = credit > 0 ? credit : -Math.Abs(debit);
            }

            if (signed == 0)
            {
                statement.InformationalRowsSkipped++;
                continue;
            }

            decimal? balance = null;
            if (map.BalanceIndex >= 0 && TryParseMoney(Get(cols, map.BalanceIndex), out var bal, out _))
            {
                balance = bal;
            }

            if (map.AccountIndex >= 0 && string.IsNullOrWhiteSpace(statement.AccountNumber))
            {
                var account = Get(cols, map.AccountIndex);
                if (!string.IsNullOrWhiteSpace(account))
                {
                    statement.AccountNumber = account;
                }
            }

            statement.Transactions.Add(new ParsedStatementTransaction
            {
                DateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
                Amount = Math.Abs(signed),
                IsCredit = signed > 0,
                Description = description,
                BalanceAfter = balance
            });
        }

        FinalizeStatement(statement);
        return statement;
    }

    public static ParsedStatement ParseOfx(string text)
    {
        var statement = new ParsedStatement { BankFormat = "FNB-OFX" };

        foreach (Match block in OfxTransactionBlockRegex().Matches(text))
        {
            string? type = null;
            string? posted = null;
            string? amount = null;
            string? memo = null;
            string? name = null;

            foreach (Match field in OfxFieldRegex().Matches(block.Groups[1].Value))
            {
                var tag = field.Groups[1].Value.ToUpperInvariant();
                var value = field.Groups[2].Value.Trim();
                switch (tag)
                {
                    case "TRNTYPE":
                        type = value;
                        break;
                    case "DTPOSTED":
                        posted = value;
                        break;
                    case "TRNAMT":
                        amount = value;
                        break;
                    case "MEMO":
                        memo = value;
                        break;
                    case "NAME":
                        name = value;
                        break;
                }
            }

            if (posted is null || amount is null || !TryParseOfxDate(posted, out var date) || !TryParseMoney(amount, out var signed, out _))
            {
                continue;
            }

            if (signed == 0)
            {
                statement.InformationalRowsSkipped++;
                continue;
            }

            var description = string.IsNullOrWhiteSpace(memo) ? name ?? type ?? "Transaction" : memo;
            statement.Transactions.Add(new ParsedStatementTransaction
            {
                DateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
                Amount = Math.Abs(signed),
                IsCredit = signed > 0,
                Description = description
            });
        }

        FinalizeStatement(statement);
        return statement;
    }

    private static void FinalizeStatement(ParsedStatement statement)
    {
        statement.Transactions.Sort((a, b) => a.DateUtc.CompareTo(b.DateUtc));

        if (statement.Transactions.Count == 0)
        {
            return;
        }

        statement.PeriodStartUtc = statement.Transactions[0].DateUtc;
        statement.PeriodEndUtc = statement.Transactions[^1].DateUtc;
        statement.Transactions[^1].IsFinalRow = true;

        var lastWithBalance = statement.Transactions.LastOrDefault(t => t.BalanceAfter is not null);
        if (lastWithBalance?.BalanceAfter is not null)
        {
            statement.ClosingBalance = lastWithBalance.BalanceAfter;
        }

        var firstWithBalance = statement.Transactions.FirstOrDefault(t => t.BalanceAfter is not null);
        if (firstWithBalance?.BalanceAfter is not null)
        {
            var signed = firstWithBalance.IsCredit ? firstWithBalance.Amount : -firstWithBalance.Amount;
            statement.OpeningBalance = firstWithBalance.BalanceAfter.Value - signed;
        }
    }

    private static string ReadText(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = reader.ReadToEnd();
        if (text.Contains('\0'))
        {
            stream.Position = 0;
            using var unicode = new StreamReader(stream, Encoding.Unicode, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return unicode.ReadToEnd();
        }

        return text;
    }

    private static List<string> SplitLines(string text)
    {
        return text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
    }

    private static int FindHeaderRow(List<string> lines)
    {
        for (var i = 0; i < Math.Min(lines.Count, 25); i++)
        {
            var cols = ParseCsvLine(lines[i]).Select(NormalizeHeader).ToList();
            var hasDate = cols.Any(c => DateHeaders.Contains(c));
            var hasDesc = cols.Any(c => DescriptionHeaders.Contains(c));
            var hasAmount = cols.Any(c => AmountHeaders.Contains(c) || DebitHeaders.Contains(c) || CreditHeaders.Contains(c));
            if (hasDate && hasDesc && hasAmount)
            {
                return i;
            }
        }

        return -1;
    }

    private readonly record struct ColumnMap(
        int DateIndex,
        int DescriptionIndex,
        int AmountIndex,
        int DebitIndex,
        int CreditIndex,
        int BalanceIndex,
        int AccountIndex);

    private static ColumnMap MapColumns(List<string> header)
    {
        return new ColumnMap(
            IndexOf(header, DateHeaders),
            IndexOf(header, DescriptionHeaders),
            IndexOf(header, AmountHeaders),
            IndexOf(header, DebitHeaders),
            IndexOf(header, CreditHeaders),
            IndexOf(header, BalanceHeaders),
            IndexOf(header, AccountHeaders));
    }

    private static int IndexOf(List<string> header, string[] names)
    {
        for (var i = 0; i < header.Count; i++)
        {
            if (names.Contains(NormalizeHeader(header[i])))
            {
                return i;
            }
        }

        return -1;
    }

    private static string NormalizeHeader(string value)
        => value.Trim().Trim('"').ToLowerInvariant();

    private static string Get(List<string> cols, int index)
        => index >= 0 && index < cols.Count ? cols[index] : "";

    private static List<string> ParseCsvLine(string line)
    {
        var delimiter = line.Count(c => c == ';') > line.Count(c => c == ',') ? ';' : ',';
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        result.Add(current.ToString());
        return result;
    }

    private static bool TryParseDate(string text, out DateTime date)
    {
        text = text.Trim().Trim('"');
        var formats = new[]
        {
            "yyyy/MM/dd", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy",
            "dd MMM yyyy", "d MMM yyyy", "dd MMMM yyyy",
            "MM/dd/yyyy"
        };

        return DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
               || DateTime.TryParse(text, CultureInfo.GetCultureInfo("en-ZA"), DateTimeStyles.None, out date)
               || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseOfxDate(string text, out DateTime date)
    {
        text = text.Trim();
        if (text.Length >= 8
            && DateTime.TryParseExact(text[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        date = default;
        return false;
    }

    private static bool TryParseMoney(string text, out decimal amount, out bool markedCredit)
    {
        markedCredit = false;
        amount = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim().Trim('"');
        markedCredit = trimmed.Contains("Cr", StringComparison.OrdinalIgnoreCase);
        var markedDebit = trimmed.Contains("Dr", StringComparison.OrdinalIgnoreCase);
        trimmed = trimmed.Replace("R", "", StringComparison.OrdinalIgnoreCase)
            .Replace("ZAR", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Cr", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Dr", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "")
            .Trim();

        var negative = trimmed.StartsWith('(') && trimmed.EndsWith(')');
        trimmed = trimmed.Trim('(', ')');

        if (trimmed.Contains(',') && trimmed.Contains('.'))
        {
            trimmed = trimmed.Replace(",", "");
        }
        else if (trimmed.Contains(',') && !trimmed.Contains('.'))
        {
            trimmed = trimmed.Replace(',', '.');
        }

        if (!decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            return false;
        }

        if (negative || markedDebit)
        {
            amount = -Math.Abs(amount);
        }
        else if (markedCredit)
        {
            amount = Math.Abs(amount);
        }

        return true;
    }
}
