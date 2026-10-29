using CoinStack.Data.Entities;

namespace CoinStack.Services.Import;

public sealed class RecurringProposal
{
    public string MerchantName { get; set; } = "";

    public string NormalizedKey { get; set; } = "";

    public string CategoryName { get; set; } = "";

    public decimal MedianAmount { get; set; }

    public SubscriptionCycle Cycle { get; set; } = SubscriptionCycle.Monthly;

    public int DebitOrderDay { get; set; } = 1;

    public int OccurrenceCount { get; set; }

    public bool CreateSubscription { get; set; } = true;

    public bool CreateBucket { get; set; } = true;

    /// <summary>A subscription with this name already exists; importing links to it and leaves its cost alone.</summary>
    public bool SubscriptionExists { get; set; }

    /// <summary>A bucket with this name already exists; importing links to it and leaves its allocation alone.</summary>
    public bool BucketExists { get; set; }

    public decimal MonthlyAllocation =>
        Math.Round(SubscriptionCosts.MonthlyEquivalent(Cycle, MedianAmount), 2, MidpointRounding.AwayFromZero);
}

public static class RecurringMerchantDetector
{
    private static readonly HashSet<string> RecurringKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ONLYFANS", "NETFLIX", "DSTV", "SPOTIFY", "DISNEY", "SHOWMAX", "APPLE", "GOOGLE",
        "CURSOR", "OPENAI", "OPENROUTER", "GITHUB", "STEAM", "WEMOD", "PAYPAL",
        "WESBANK", "FNBCC", "FNB_ACCOUNT_FEE", "FNB_SERVICE_FEES", "RUNPOD", "TENSORDOCK", "COMFYORG"
    };

    private static readonly HashSet<string> SkipCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Groceries", "Dining", "Shopping", "Salary", "Freelance", "Transfer"
    };

    public static List<RecurringProposal> Detect(IReadOnlyList<ImportCandidate> candidates)
    {
        var expenses = candidates
            .Where(c => c.Type == TransactionType.Expense
                        && !c.IsPassThrough
                        && !c.IsAvoidableFee
                        && !string.IsNullOrWhiteSpace(c.NormalizedKey))
            .ToList();

        return DetectFromRows(
            expenses.Select(c => new RecurringRow(
                c.NormalizedKey,
                c.MerchantName,
                c.CategoryName ?? "Shopping",
                c.Amount,
                c.DateUtc,
                c.RawDescription ?? c.Description)).ToList());
    }

    public static List<RecurringProposal> DetectFromTransactions(IReadOnlyList<Transaction> transactions)
    {
        var rows = new List<RecurringRow>();
        foreach (var tx in transactions)
        {
            if (tx.Type != TransactionType.Expense)
            {
                continue;
            }

            if (TransactionConventions.LooksLikeSalary(tx.Description)
                || (tx.Notes is not null && TransactionConventions.LooksLikeSalary(tx.Notes)))
            {
                continue;
            }

            var info = MerchantNormalizer.Normalize(tx.Description, TransactionType.Expense);
            if (info.IsAvoidableFee)
            {
                continue;
            }

            rows.Add(new RecurringRow(
                info.NormalizedKey,
                info.CanonicalName,
                tx.Category?.Name ?? info.CategoryName,
                tx.Amount,
                tx.OccurredAtUtc,
                tx.Notes ?? tx.Description));
        }

        return DetectFromRows(rows);
    }

    private readonly record struct RecurringRow(
        string Key,
        string Name,
        string Category,
        decimal Amount,
        DateTime DateUtc,
        string Raw);

    private static List<RecurringProposal> DetectFromRows(List<RecurringRow> rows)
    {
        var proposals = new List<RecurringProposal>();

        foreach (var group in rows.GroupBy(r => r.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() < 2)
            {
                continue;
            }

            var sample = group.First();
            var looksDebitOrder = group.Any(r => LooksLikeDebitOrder(r.Raw));
            var knownKey = RecurringKeys.Contains(group.Key);

            if (!knownKey && !looksDebitOrder && SkipCategories.Contains(sample.Category))
            {
                continue;
            }

            var months = group.Select(r => new DateTime(r.DateUtc.Year, r.DateUtc.Month, 1)).Distinct().Count();
            var perMonth = months > 0 ? group.Count() / (double)months : group.Count();
            if (perMonth > 2.5 && !knownKey && !looksDebitOrder)
            {
                continue;
            }

            var amounts = group.Select(r => r.Amount).OrderBy(a => a).ToList();
            var median = Median(amounts);
            if (median <= 0)
            {
                continue;
            }

            var maxDeviation = amounts.Max(a => Math.Abs(a - median));
            if (amounts.Count > 1 && maxDeviation / median > 0.15m && !knownKey)
            {
                continue;
            }

            if (months < 2 && !HasMonthlySpacing(group.Select(r => r.DateUtc).ToList()) && !knownKey)
            {
                continue;
            }

            var (cycle, cost) = ResolveCycleAndCost(group.ToList(), median);
            var debitDay = group
                .Select(r => r.DateUtc.Day)
                .GroupBy(d => d)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .First();

            proposals.Add(new RecurringProposal
            {
                MerchantName = sample.Name,
                NormalizedKey = group.Key,
                CategoryName = sample.Category,
                MedianAmount = Math.Round(cost, 2, MidpointRounding.AwayFromZero),
                Cycle = cycle,
                DebitOrderDay = Math.Clamp(debitDay, 1, 28),
                OccurrenceCount = group.Count(),
                CreateSubscription = true,
                CreateBucket = true
            });
        }

        proposals.Sort((a, b) => b.MonthlyAllocation.CompareTo(a.MonthlyAllocation));
        return proposals;
    }

    private static bool LooksLikeDebitOrder(string raw)
    {
        return raw.Contains("DEBIT ORDER", StringComparison.OrdinalIgnoreCase)
               || raw.Contains("DEBICHECK", StringComparison.OrdinalIgnoreCase)
               || raw.Contains("SUBSCRIPTION", StringComparison.OrdinalIgnoreCase)
               || raw.Contains("DEBITORD", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasMonthlySpacing(List<DateTime> dates)
    {
        if (dates.Count < 2)
        {
            return false;
        }

        var ordered = dates.OrderBy(d => d).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            var days = (ordered[i] - ordered[i - 1]).TotalDays;
            if (days is >= 25 and <= 35)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Works out how often the merchant charges, from the typical gap between charges, and the cost
    /// per cycle. A merchant that charges more than once in a month at uneven spacing (two plans, or a
    /// top-up) is monthly, costing the month's total rather than one charge.
    /// </summary>
    private static (SubscriptionCycle Cycle, decimal Cost) ResolveCycleAndCost(List<RecurringRow> rows, decimal medianCharge)
    {
        var ordered = rows.Select(r => r.DateUtc).OrderBy(d => d).ToList();
        if (ordered.Count < 2)
        {
            return (SubscriptionCycle.Monthly, medianCharge);
        }

        var gaps = new List<double>();
        for (var i = 1; i < ordered.Count; i++)
        {
            gaps.Add((ordered[i] - ordered[i - 1]).TotalDays);
        }

        var medianGap = Median(gaps.OrderBy(g => g).Select(g => (decimal)g).ToList());

        bool MostGapsBetween(decimal low, decimal high) =>
            gaps.Count(g => (decimal)g >= low && (decimal)g <= high) >= Math.Ceiling(gaps.Count * 0.75);

        if (medianGap is >= 5 and <= 10 && MostGapsBetween(5, 10))
        {
            return (SubscriptionCycle.Weekly, medianCharge);
        }

        if (medianGap is >= 11 and <= 18 && MostGapsBetween(11, 18))
        {
            return (SubscriptionCycle.Fortnightly, medianCharge);
        }

        if (medianGap is >= 75 and <= 110)
        {
            return (SubscriptionCycle.Quarterly, medianCharge);
        }

        if (medianGap is >= 150 and <= 210)
        {
            return (SubscriptionCycle.HalfYearly, medianCharge);
        }

        if (medianGap >= 300)
        {
            return (SubscriptionCycle.Yearly, medianCharge);
        }

        if (medianGap <= 45)
        {
            var monthlyTotals = rows
                .GroupBy(r => (r.DateUtc.Year, r.DateUtc.Month))
                .Select(g => g.Sum(r => r.Amount))
                .OrderBy(total => total)
                .ToList();

            return (SubscriptionCycle.Monthly, Median(monthlyTotals));
        }

        // Spacing between the standard cycles (e.g. every two months): book the monthly average.
        return (SubscriptionCycle.Monthly, medianCharge * 30.44m / medianGap);
    }

    private static decimal Median(List<decimal> ordered)
    {
        var n = ordered.Count;
        if (n == 0)
        {
            return 0;
        }

        if (n % 2 == 1)
        {
            return ordered[n / 2];
        }

        return (ordered[n / 2 - 1] + ordered[n / 2]) / 2m;
    }
}
