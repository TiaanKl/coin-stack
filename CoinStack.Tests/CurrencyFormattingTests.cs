using CoinStack.Data.Entities;
using CoinStack.Mobile.Core;
using CoinStack.Services;
using Xunit;

namespace CoinStack.Tests;

public sealed class CurrencyFormattingTests
{
    // en-ZA groups digits with a no-break space.
    private const char Nbsp = ' ';

    [Fact]
    public void Negative_Rand_Puts_The_Minus_Before_The_Symbol()
    {
        Assert.Equal($"-R1{Nbsp}234.56", CurrencyFormatting.FormatCurrency(-1234.56m, "ZAR"));
        Assert.Equal($"R1{Nbsp}234.56", CurrencyFormatting.FormatCurrency(1234.56m, "ZAR"));
    }

    [Fact]
    public void Negative_Dollars_And_Euros_Are_Consistent()
    {
        Assert.Equal("-$1,234.56", CurrencyFormatting.FormatCurrency(-1234.56m, "USD"));
        Assert.StartsWith("-1", CurrencyFormatting.FormatCurrency(-1234.56m, "EUR"));
        Assert.EndsWith("€", CurrencyFormatting.FormatCurrency(-1234.56m, "EUR"));
    }

    [Fact]
    public void Injected_Formatter_Matches_The_Static_One_Whatever_The_Current_Culture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            var formatter = new MoneyFormatter();

            Assert.Equal(CurrencyFormatting.FormatCurrency(-1234.56m, "ZAR"), formatter.Format("ZAR", -1234.56m));
            Assert.Equal("R", formatter.Symbol("ZAR"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Mobile_Display_Matches_The_Shared_Formatter()
    {
        foreach (var code in new[] { "ZAR", "USD", "EUR", "GBP", "CAD", "AUD" })
        {
            Assert.Equal(CurrencyFormatting.FormatCurrency(-98765.4m, code), MoneyDisplay.Format(code, -98765.4m));
            Assert.Equal(CurrencyFormatting.FormatCurrency(42m, code, 0), MoneyDisplay.Format(code, 42m, 0));
        }
    }

    [Fact]
    public void New_Settings_Default_To_Rand()
    {
        Assert.Equal("ZAR", new AppSettings().Currency);
    }
}
