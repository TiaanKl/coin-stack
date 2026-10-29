using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CoinStack.Data.Entities;
using CoinStack.Services.Import;
using Xunit;

namespace CoinStack.Tests;

public sealed class ImportFingerprintTests
{
    private static readonly DateTime Day = new(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Identical_Rows_On_The_Same_Day_Get_Distinct_Fingerprints()
    {
        var file = TwoCoffees();

        BankStatementImportService.AssignFingerprints(file);

        Assert.NotEqual(file[0].Fingerprint, file[1].Fingerprint);
    }

    [Fact]
    public void Reimporting_The_Same_File_Matches_Every_Row()
    {
        var first = TwoCoffees();
        BankStatementImportService.AssignFingerprints(first);

        var again = TwoCoffees();
        BankStatementImportService.AssignFingerprints(again);
        BankStatementImportService.MarkExisting(again, first.Select(c => c.Fingerprint));

        Assert.All(again, c => Assert.True(c.AlreadyExists));
    }

    [Fact]
    public void A_Third_Identical_Row_In_A_Later_File_Is_New()
    {
        var stored = TwoCoffees();
        BankStatementImportService.AssignFingerprints(stored);

        var later = TwoCoffees().Append(Coffee()).ToList();
        BankStatementImportService.AssignFingerprints(later);
        BankStatementImportService.MarkExisting(later, stored.Select(c => c.Fingerprint));

        Assert.Equal(2, later.Count(c => c.AlreadyExists));
        Assert.False(later[2].AlreadyExists);
    }

    [Fact]
    public void Fingerprint_Ignores_Normaliser_Output_And_Culture()
    {
        var a = Coffee();
        BankStatementImportService.AssignFingerprints([a]);

        var renamed = Coffee();
        renamed.Description = "Vida e Caffè";          // the normaliser's canonical name changed
        renamed.Type = TransactionType.Transfer;        // or a classification rule changed

        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // "35,00" in the old scheme
            BankStatementImportService.AssignFingerprints([renamed]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        Assert.Equal(a.Fingerprint, renamed.Fingerprint);
    }

    [Fact]
    public void Legacy_Fingerprints_Are_Matched_As_A_Multiset()
    {
        // Rows imported before v2 stored hash(date|amount|type|CANONICAL), with no occurrence number.
        var legacy = LegacyHash("20260814|35.00|Expense|VIDA E CAFFE");
        var stored = new[] { legacy, legacy };

        var file = TwoCoffees().Append(Coffee()).ToList();
        BankStatementImportService.AssignFingerprints(file);
        BankStatementImportService.MarkExisting(file, stored);

        Assert.Equal(2, file.Count(c => c.AlreadyExists));
    }

    [Fact]
    public void Legacy_Fingerprints_Written_With_A_Decimal_Comma_Still_Match()
    {
        var stored = new[] { LegacyHash("20260814|35,00|Expense|VIDA E CAFFE") };

        var file = new List<ImportCandidate> { Coffee() };
        BankStatementImportService.AssignFingerprints(file);
        BankStatementImportService.MarkExisting(file, stored);

        Assert.True(file[0].AlreadyExists);
    }

    private static List<ImportCandidate> TwoCoffees() => [Coffee(), Coffee()];

    private static ImportCandidate Coffee() => new()
    {
        DateUtc = Day,
        Amount = 35m,
        IsCredit = false,
        Type = TransactionType.Expense,
        Description = "Vida e Caffe",
        RawDescription = "POS Purchase Vida E Caffe Sandton 1234"
    };

    private static string LegacyHash(string raw)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..32];
}
