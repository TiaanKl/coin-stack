using CoinStack.Services.Import;
using Xunit;

namespace CoinStack.Tests;

public sealed class MoneyParsingTests
{
    [Theory]
    [InlineData("1,234.56 Cr", 1234.56, true)]
    [InlineData("1,234.56Cr", 1234.56, true)]
    [InlineData("1 234,56", 1234.56, false)]
    [InlineData("1\u00A0234,56 Dr", -1234.56, false)]
    [InlineData("1,500", 1500, false)]
    [InlineData("12,345,678", 12345678, false)]
    [InlineData("1,50", 1.50, false)]
    [InlineData("1.234,56", 1234.56, false)]
    [InlineData("R1,234.56", 1234.56, false)]
    [InlineData("R -350.00", -350, false)]
    [InlineData("-R350.00", -350, false)]
    [InlineData("(350.00)", -350, false)]
    [InlineData("100.00-", -100, false)]
    [InlineData("ZAR 100", 100, false)]
    [InlineData("\"-199.00\"", -199, false)]
    public void TryParseMoney_Handles_Bank_Formats(string text, double expected, bool expectCredit)
    {
        Assert.True(FnbTransactionHistoryParser.TryParseMoney(text, out var amount, out var credit));
        Assert.Equal((decimal)expected, amount);
        Assert.Equal(expectCredit, credit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("R")]
    [InlineData("Cr")]
    [InlineData("n/a")]
    public void TryParseMoney_Rejects_Non_Amounts(string text)
    {
        Assert.False(FnbTransactionHistoryParser.TryParseMoney(text, out _, out _));
    }

    [Fact]
    public void Csv_Credit_Suffix_Row_Is_Imported_Not_Skipped()
    {
        // Before the fix "1,234.56 Cr" became "1234.56C", failed to parse, and the row vanished.
        var csv = """
            Date,Description,Amount,Balance
            2026/08/01,Coffee,"-35.00","1,200.00 Cr"
            2026/08/02,Refund,"1,234.56 Cr","2,434.56 Cr"
            """;

        var statement = FnbTransactionHistoryParser.ParseCsv(csv);

        Assert.Equal(2, statement.Transactions.Count);
        Assert.True(statement.Transactions[1].IsCredit);
        Assert.Equal(1234.56m, statement.Transactions[1].Amount);
        Assert.Equal(2434.56m, statement.ClosingBalance);
    }
}
