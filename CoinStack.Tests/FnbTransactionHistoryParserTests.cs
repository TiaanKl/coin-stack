using System.Text;
using CoinStack.Services.Import;
using Xunit;

namespace CoinStack.Tests;

public sealed class FnbTransactionHistoryParserTests
{
    [Fact]
    public void Csv_With_Signed_Amount_And_Balance_Fills_The_Gap_After_A_Lagged_Statement()
    {
        var csv = """
            Date,Description,Amount,Balance
            2026/07/02,NETFLIX,-199.00,4120.50
            2026/08/02,NETFLIX,-199.00,3921.50
            2026/08/15,Salary Multicat,25000.00,28921.50
            """;

        var statement = FnbTransactionHistoryParser.ParseCsv(csv);

        Assert.Equal("FNB-CSV", statement.BankFormat);
        Assert.Equal(3, statement.Transactions.Count);
        Assert.Equal(new DateTime(2026, 7, 2), statement.PeriodStartUtc!.Value.Date);
        Assert.Equal(new DateTime(2026, 8, 15), statement.PeriodEndUtc!.Value.Date);
        Assert.Equal(28921.50m, statement.ClosingBalance);
        Assert.False(statement.Transactions[0].IsCredit);
        Assert.True(statement.Transactions[2].IsCredit);
        Assert.Equal(25000m, statement.Transactions[2].Amount);
    }

    [Fact]
    public void Csv_Debit_Credit_Columns_Are_Supported()
    {
        var csv = """
            Transaction Date,Description,Debit,Credit,Balance
            01/07/2026,DSTV,899.00,,1000.00
            25/07/2026,Salary Multicat,,20000.00,21000.00
            """;

        var statement = FnbTransactionHistoryParser.ParseCsv(csv);

        Assert.Equal(2, statement.Transactions.Count);
        Assert.False(statement.Transactions[0].IsCredit);
        Assert.Equal(899m, statement.Transactions[0].Amount);
        Assert.True(statement.Transactions[1].IsCredit);
        Assert.Equal(20000m, statement.Transactions[1].Amount);
        Assert.Equal(21000m, statement.ClosingBalance);
    }

    [Fact]
    public void Ofx_Debit_And_Credit_Are_Parsed()
    {
        var ofx = """
            OFXHEADER:100
            <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>
            <STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20260712000000<TRNAMT>-55.00<MEMO>Spotify</STMTTRN>
            <STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20260825000000<TRNAMT>25000.00<MEMO>Salary Multicat</STMTTRN>
            </BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
            """;

        var statement = FnbTransactionHistoryParser.ParseOfx(ofx);

        Assert.Equal(2, statement.Transactions.Count);
        Assert.Equal("Spotify", statement.Transactions[0].Description);
        Assert.False(statement.Transactions[0].IsCredit);
        Assert.True(statement.Transactions[1].IsCredit);
    }

    [Fact]
    public void History_Feed_Accepts_Csv_And_Ofx_Not_Pdf()
    {
        var feed = new FnbHistoryBankFeed();
        Assert.True(feed.CanParse("fnb-july.csv"));
        Assert.True(feed.CanParse("export.OFX"));
        Assert.False(feed.CanParse("statement.pdf"));
    }

    [Fact]
    public void Composite_Feed_Routes_Csv()
    {
        var feed = new CompositeBankFeed([new FnbPdfBankFeed(), new FnbHistoryBankFeed()]);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Date,Description,Amount\n2026/08/01,NETFLIX,-199.00\n"));
        var statement = feed.Parse(stream, "history.csv");
        Assert.Equal("FNB-CSV", statement.BankFormat);
        Assert.Single(statement.Transactions);
    }
}
