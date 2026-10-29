namespace CoinStack.Services;

/// <summary>Turns payments spread over several months into a figure per month.</summary>
public static class MonthlyTotals
{
    private const double DaysPerMonth = 365.25 / 12;

    /// <summary>
    /// The usual amount per month: the median of each month's total, over the months anything arrived.
    /// Three monthly salaries give one salary, two fortnightly payments in a month add up to one, and a
    /// once-a-year bonus doesn't lift the figure.
    /// </summary>
    public static decimal Typical(IEnumerable<(DateTime Date, decimal Amount)> payments)
    {
        var totals = payments
            .GroupBy(p => (p.Date.Year, p.Date.Month))
            .Select(g => g.Sum(p => p.Amount))
            .OrderBy(t => t)
            .ToList();

        if (totals.Count == 0)
        {
            return 0m;
        }

        var middle = totals.Count / 2;
        var median = totals.Count % 2 == 1 ? totals[middle] : (totals[middle - 1] + totals[middle]) / 2m;
        return Math.Round(median, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>How many months a date range covers, to the nearest month and at least one.</summary>
    public static int MonthsCovered(DateTime first, DateTime last)
        => Math.Max(1, (int)Math.Round(((last.Date - first.Date).TotalDays + 1) / DaysPerMonth));
}
