using CoinStack.Data.Entities;

namespace CoinStack.Services;

/// <summary>Everything a waitlist item's readiness score depends on, gathered by <see cref="WaitlistService"/>.</summary>
/// <param name="SavingsRate">Share of the savings bucket's target saved this period, or null with no savings bucket.</param>
/// <param name="SavingsAvailable">Money in savings that could pay for the item.</param>
public sealed record WaitlistReadinessInputs(
    decimal ItemCost,
    int BucketCount,
    int BucketsOverBudget,
    decimal? SavingsRate,
    decimal MonthlyIncome,
    decimal MonthlyDebtPayments,
    int DebtCount,
    decimal SavingsAvailable,
    EmotionTag? Emotion,
    WaitlistPriority Priority,
    double DaysWaited);

/// <summary>
/// Scores out of 100 how ready you are to buy a waitlist item. The item's price is the largest single
/// part (25 points): a R50 item and a R50 000 item are not equally affordable, even when everything
/// else about your finances is the same.
/// </summary>
public static class WaitlistReadiness
{
    /// <summary>The highest score an item can get when its price is more than your income and savings combined.</summary>
    public const int UnaffordableCap = 39;

    public static int Calculate(WaitlistReadinessInputs inputs)
    {
        var total = AffordabilityScore(inputs.ItemCost, inputs.MonthlyIncome, inputs.SavingsAvailable)
                    + BudgetScore(inputs.BucketCount, inputs.BucketsOverBudget)
                    + SavingsScore(inputs.SavingsRate)
                    + DebtScore(inputs.MonthlyIncome, inputs.MonthlyDebtPayments, inputs.DebtCount)
                    + EmotionScore(inputs.Emotion)
                    + PriorityScore(inputs.Priority)
                    + TimeScore(inputs.DaysWaited);

        var cushion = inputs.MonthlyIncome + inputs.SavingsAvailable;
        if (cushion > 0m && inputs.ItemCost > cushion)
        {
            total = Math.Min(total, UnaffordableCap);
        }

        return Math.Clamp(total, 0, 100);
    }

    /// <summary>Up to 25 points, by the price as a share of a month's income plus savings on hand.</summary>
    internal static int AffordabilityScore(decimal cost, decimal monthlyIncome, decimal savingsAvailable)
    {
        if (cost <= 0m)
        {
            return 25;
        }

        var cushion = monthlyIncome + Math.Max(0m, savingsAvailable);
        if (cushion <= 0m)
        {
            // No income or savings on record, so the price can't be judged either way.
            return 10;
        }

        var share = cost / cushion;
        return share <= 0.02m ? 25
             : share <= 0.05m ? 21
             : share <= 0.10m ? 16
             : share <= 0.25m ? 10
             : share <= 0.50m ? 5
             : 0;
    }

    private static int BudgetScore(int bucketCount, int bucketsOverBudget)
    {
        return bucketCount == 0
            ? 8
            : (int)Math.Round(15.0 * (1.0 - ((double)bucketsOverBudget / bucketCount)));
    }

    private static int SavingsScore(decimal? savingsRate)
    {
        return savingsRate switch
        {
            null => 8,
            >= 1.0m => 15,
            >= 0.75m => 13,
            >= 0.5m => 10,
            >= 0.25m => 6,
            _ => 2
        };
    }

    private static int DebtScore(decimal monthlyIncome, decimal monthlyDebtPayments, int debtCount)
    {
        if (monthlyIncome <= 0m || monthlyDebtPayments <= 0m)
        {
            return debtCount == 0 ? 15 : 8;
        }

        var debtToIncome = monthlyDebtPayments / monthlyIncome;
        return debtToIncome < 0.15m ? 15
             : debtToIncome < 0.25m ? 12
             : debtToIncome < 0.35m ? 8
             : debtToIncome < 0.50m ? 4
             : 1;
    }

    private static int EmotionScore(EmotionTag? emotion)
    {
        return emotion switch
        {
            EmotionTag.Motivated or EmotionTag.Proud => 10,
            EmotionTag.Excited => 8,
            EmotionTag.Neutral => 7,
            EmotionTag.Bored or EmotionTag.Tired => 4,
            EmotionTag.Stressed or EmotionTag.Anxious => 3,
            EmotionTag.Impulsive or EmotionTag.Guilty => 1,
            _ => 5
        };
    }

    private static int PriorityScore(WaitlistPriority priority)
    {
        return priority switch
        {
            WaitlistPriority.High => 10,
            WaitlistPriority.Medium => 7,
            _ => 3
        };
    }

    private static int TimeScore(double daysWaited)
    {
        return daysWaited >= 30 ? 10
             : daysWaited >= 14 ? 8
             : daysWaited >= 7 ? 6
             : daysWaited >= 3 ? 3
             : 0;
    }
}
