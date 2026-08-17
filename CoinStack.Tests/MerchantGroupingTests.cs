using CoinStack.Data.Entities;
using CoinStack.Services.Import;
using Xunit;

namespace CoinStack.Tests;

public sealed class MerchantGroupingTests
{
    [Theory]
    [InlineData("POS Purchase 11.50 Onlyfans*J 412752*5585 24 Apr", "OnlyFans", "ONLYFANS", "Entertainment")]
    [InlineData("POS Purchase 14.95 Of 412752*5585 24 Apr", "OnlyFans", "ONLYFANS", "Entertainment")]
    [InlineData("POS Purchase 18.40 Of 412752*5585 24 Apr", "OnlyFans", "ONLYFANS", "Entertainment")]
    [InlineData("POS Purchase 28.75 Onlyfans.Com 412752*5585 24 Apr", "OnlyFans", "ONLYFANS", "Entertainment")]
    [InlineData("POS Purchase 46.00 Onlyfans.Com* 412752*5585 24 Apr", "OnlyFans", "ONLYFANS", "Entertainment")]
    [InlineData("POS Purchase 5.23 Of 412752*5585 02 Jun", "OnlyFans", "ONLYFANS", "Entertainment")]
    public void OnlyFans_Variants_Normalize_To_Same_Merchant(string rawDescription, string expectedName, string expectedKey, string expectedCategory)
    {
        var info = MerchantNormalizer.Normalize(rawDescription, TransactionType.Expense);

        Assert.Equal(expectedName, info.CanonicalName);
        Assert.Equal(expectedKey, info.NormalizedKey);
        Assert.Equal(expectedCategory, info.CategoryName);
    }

    [Theory]
    [InlineData("POS Purchase 310.10 Steamgames.C 412752*5585 26 May", "Steam", "STEAM")]
    [InlineData("Digital Content Voucher Steam 26053017560837750", "Steam", "STEAM")]
    [InlineData("POS Purchase 46.31 Steam Purchas 412752*5585 29 May", "Steam", "STEAM")]
    [InlineData("POS Purchase 375.00 Steam Purcha 412752*5585 29 May", "Steam", "STEAM")]
    public void Steam_Variants_Normalize_To_Same_Merchant(string rawDescription, string expectedName, string expectedKey)
    {
        var info = MerchantNormalizer.Normalize(rawDescription, TransactionType.Expense);

        Assert.Equal(expectedName, info.CanonicalName);
        Assert.Equal(expectedKey, info.NormalizedKey);
        Assert.Equal("Entertainment", info.CategoryName);
    }

    [Theory]
    [InlineData("POS Purchase Pick N Pay Asap 412752*5585 17 Mar", "Pick n Pay", "PICK_N_PAY")]
    [InlineData("POS Purchase PNP Fam Graanendal 412752*5585 25 Mar", "Pick n Pay", "PICK_N_PAY")]
    [InlineData("POS Purchase Pick N Pay Asap 412752*5585 29 May", "Pick n Pay", "PICK_N_PAY")]
    public void PickNPay_Variants_Normalize_To_PickNPay(string rawDescription, string expectedName, string expectedKey)
    {
        var info = MerchantNormalizer.Normalize(rawDescription, TransactionType.Expense);

        Assert.Equal(expectedName, info.CanonicalName);
        Assert.Equal(expectedKey, info.NormalizedKey);
        Assert.Equal("Groceries", info.CategoryName);
    }

    [Theory]
    [InlineData("POS Purchase 189.99 Google Fortr 412752*5585 28 Apr", "Google Play & Services", "GOOGLE")]
    [InlineData("POS Purchase 429.99 Google One 412752*5585 26 May", "Google Play & Services", "GOOGLE")]
    [InlineData("POS Purchase 17.99 Google Slime 412752*5585 29 Mar", "Google Play & Services", "GOOGLE")]
    public void Google_Variants_Normalize_To_Google(string rawDescription, string expectedName, string expectedKey)
    {
        var info = MerchantNormalizer.Normalize(rawDescription, TransactionType.Expense);

        Assert.Equal(expectedName, info.CanonicalName);
        Assert.Equal(expectedKey, info.NormalizedKey);
    }

    [Fact]
    public void Avoidable_Bank_Fees_Are_Identified_And_Flagged()
    {
        var unpaidFee = MerchantNormalizer.Normalize("#Item Paid No Funds 3 Items On 26/03/17", TransactionType.Expense);
        Assert.True(unpaidFee.IsAvoidableFee);
        Assert.Equal("FNB Unpaid Item Fee", unpaidFee.CanonicalName);
        Assert.Equal("Banking Fees", unpaidFee.CategoryName);

        var declinedFee = MerchantNormalizer.Normalize("#Debit Card POS Unsuccessful F #Fee Declined Purch Tran 4127525047865585", TransactionType.Expense);
        Assert.True(declinedFee.IsAvoidableFee);
        Assert.Equal("FNB Declined Transaction Fee", declinedFee.CanonicalName);
        Assert.Equal("Banking Fees", declinedFee.CategoryName);
    }

    [Fact]
    public void Debt_And_Vehicle_Finance_Are_Classified()
    {
        var wesbank = MerchantNormalizer.Normalize("DebiCheck Internal D/O WesBank_Fi8540077536", TransactionType.Expense);
        Assert.Equal("WesBank Vehicle Finance", wesbank.CanonicalName);
        Assert.Equal("Debt", wesbank.CategoryName);

        var fnbcc = MerchantNormalizer.Normalize("DebiCheck Internal D/O Fnbcc Dcre221529", TransactionType.Expense);
        Assert.Equal("FNB Credit Card", fnbcc.CanonicalName);
        Assert.Equal("Debt", fnbcc.CategoryName);
    }
}
