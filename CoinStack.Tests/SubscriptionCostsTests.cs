using CoinStack.Data.Entities;
using CoinStack.Services;
using Xunit;

namespace CoinStack.Tests;

public sealed class SubscriptionCostsTests
{
    [Theory]
    [InlineData(SubscriptionCycle.Monthly, 300, 300)]
    [InlineData(SubscriptionCycle.Quarterly, 300, 100)]
    [InlineData(SubscriptionCycle.Yearly, 1200, 100)]
    public void MonthlyEquivalent_Converts_Each_Cycle(SubscriptionCycle cycle, int cost, int expected)
    {
        Assert.Equal(expected, SubscriptionCosts.MonthlyEquivalent(cycle, cost));
    }

    [Fact]
    public void Weekly_Uses_52_Weeks_Over_12_Months()
    {
        Assert.Equal(100m * 52m / 12m, SubscriptionCosts.MonthlyEquivalent(SubscriptionCycle.Weekly, 100m));
    }

    [Theory]
    [InlineData(2026, 11)] // 30 days
    [InlineData(2027, 2)]  // 28 days
    [InlineData(2026, 12)] // 31 days
    public void Debit_Order_On_The_31st_Is_Charged_Exactly_Once_A_Month(int year, int month)
    {
        // The old forecast mapped days 29-31 onto the 28th and charged it on every one of those days.
        var sub = new Subscription { Cost = 450m, Cycle = SubscriptionCycle.Monthly, DebitOrderDay = 31 };

        var charges = Days(year, month)
            .Select(d => (Day: d, Amount: SubscriptionCosts.ForecastChargeOn(sub, d)))
            .Where(x => x.Amount > 0)
            .ToList();

        var only = Assert.Single(charges);
        Assert.Equal(DateTime.DaysInMonth(year, month), only.Day.Day);
        Assert.Equal(450m, only.Amount);
    }

    [Fact]
    public void Yearly_Subscription_Charges_Its_Monthly_Share_Not_The_Full_Price()
    {
        var sub = new Subscription { Cost = 1200m, Cycle = SubscriptionCycle.Yearly, DebitOrderDay = 15 };

        var monthTotal = Days(2026, 11).Sum(d => SubscriptionCosts.ForecastChargeOn(sub, d));

        Assert.Equal(100m, monthTotal);
    }

    [Fact]
    public void Subscription_Without_A_Debit_Day_Is_Spread_Across_The_Month()
    {
        var sub = new Subscription { Cost = 300m, Cycle = SubscriptionCycle.Monthly, DebitOrderDay = null };

        var days = Days(2026, 11).ToList();

        Assert.All(days, d => Assert.Equal(10m, SubscriptionCosts.ForecastChargeOn(sub, d)));
        Assert.Equal(300m, days.Sum(d => SubscriptionCosts.ForecastChargeOn(sub, d)));
    }

    private static IEnumerable<DateTime> Days(int year, int month)
        => Enumerable.Range(1, DateTime.DaysInMonth(year, month))
            .Select(d => new DateTime(year, month, d, 0, 0, 0, DateTimeKind.Utc));
}
