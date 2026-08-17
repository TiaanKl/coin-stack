using CoinStack.Data.Entities;
using CoinStack.Services;
using CoinStack.Services.Import;
using Xunit;

namespace CoinStack.Tests;

public sealed class RecurringMerchantDetectorTests
{
    [Fact]
    public void Two_Monthly_Netflix_Rows_Become_A_Subscription_Proposal()
    {
        var candidates = new List<ImportCandidate>
        {
            Netflix(new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc), 199.00m),
            Netflix(new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc), 199.00m)
        };

        var proposals = RecurringMerchantDetector.Detect(candidates);

        var netflix = Assert.Single(proposals);
        Assert.Equal("NETFLIX", netflix.NormalizedKey);
        Assert.Equal(199.00m, netflix.MedianAmount);
        Assert.Equal(SubscriptionCycle.Monthly, netflix.Cycle);
        Assert.Equal(12, netflix.DebitOrderDay);
        Assert.True(netflix.CreateSubscription);
        Assert.True(netflix.CreateBucket);
    }

    [Fact]
    public void Salary_Income_Is_Never_A_Subscription()
    {
        var txs = new List<Transaction>
        {
            new()
            {
                Type = TransactionType.Income,
                Description = "Salary (Multicat)",
                Amount = 25000m,
                OccurredAtUtc = new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                Type = TransactionType.Income,
                Description = "Salary (Multicat)",
                Amount = 25000m,
                OccurredAtUtc = new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        var proposals = RecurringMerchantDetector.DetectFromTransactions(txs);
        Assert.Empty(proposals);
    }

    [Fact]
    public void Grocery_Pos_Does_Not_Become_A_Subscription()
    {
        var candidates = new List<ImportCandidate>
        {
            Grocery(new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), 412m),
            Grocery(new DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Utc), 380m),
            Grocery(new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), 450m),
            Grocery(new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc), 390m)
        };

        Assert.Empty(RecurringMerchantDetector.Detect(candidates));
    }

    [Fact]
    public void AppSettings_Default_MonthlyIncome_Is_Zero()
    {
        Assert.Equal(0m, new AppSettings().MonthlyIncome);
    }

    [Fact]
    public void Synthetic_Monthly_Income_Is_Identified()
    {
        var tx = new Transaction
        {
            Type = TransactionType.Income,
            Description = TransactionConventions.SyntheticMonthlyIncomeDescription,
            Source = TransactionConventions.AutoSource,
            Amount = 5000m
        };

        Assert.True(TransactionConventions.IsSyntheticMonthlyIncome(tx));
    }

    private static ImportCandidate Netflix(DateTime date, decimal amount) => new()
    {
        DateUtc = date,
        Amount = amount,
        Type = TransactionType.Expense,
        Description = "Netflix",
        MerchantName = "Netflix",
        NormalizedKey = "NETFLIX",
        CategoryName = "Entertainment"
    };

    private static ImportCandidate Grocery(DateTime date, decimal amount) => new()
    {
        DateUtc = date,
        Amount = amount,
        Type = TransactionType.Expense,
        Description = "Pick n Pay",
        MerchantName = "Pick n Pay",
        NormalizedKey = "PICK_N_PAY",
        CategoryName = "Groceries"
    };
}
