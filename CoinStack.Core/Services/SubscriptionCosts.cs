using CoinStack.Data.Entities;

namespace CoinStack.Services;

/// <summary>
/// One place to turn a subscription's per-cycle <see cref="Subscription.Cost"/> into money per month or
/// per day. The dashboard forecast used to treat every cost as monthly (a yearly R1 200 hit every month)
/// and mapped days 29–31 onto the 28th (a debit order on the 31st was charged four times a month).
/// </summary>
public static class SubscriptionCosts
{
    /// <summary>The cost expressed per month: weekly × 52/12, quarterly ÷ 3, yearly ÷ 12.</summary>
    public static decimal MonthlyEquivalent(SubscriptionCycle cycle, decimal cost) => cycle switch
    {
        SubscriptionCycle.Weekly => cost * 52m / 12m,
        SubscriptionCycle.Quarterly => cost / 3m,
        SubscriptionCycle.Yearly => cost / 12m,
        _ => cost
    };

    public static decimal MonthlyEquivalent(Subscription subscription)
        => MonthlyEquivalent(subscription.Cycle, subscription.Cost);

    /// <summary>
    /// What the subscription is expected to cost on <paramref name="date"/> in a cash-flow forecast.
    /// </summary>
    /// <remarks>
    /// A monthly subscription with a debit-order day is charged in full on that day, moved to the last
    /// day of short months. Quarterly and yearly subscriptions store no renewal month, so their monthly
    /// equivalent is charged on the debit day instead of guessing the month. Weekly subscriptions, and
    /// any without a debit day, are spread evenly across the days of the month.
    /// </remarks>
    public static decimal ForecastChargeOn(Subscription subscription, DateTime date)
    {
        var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);

        if (subscription.DebitOrderDay is not { } debitDay || subscription.Cycle == SubscriptionCycle.Weekly)
        {
            return MonthlyEquivalent(subscription) / daysInMonth;
        }

        var chargeDay = Math.Clamp(debitDay, 1, daysInMonth);
        return date.Day == chargeDay ? MonthlyEquivalent(subscription) : 0m;
    }
}
