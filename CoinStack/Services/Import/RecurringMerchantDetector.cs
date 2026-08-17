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

    public decimal MonthlyAllocation => Cycle switch
    {
        SubscriptionCycle.Weekly => Math.Round(MedianAmount * 4.3m, 2, MidpointRounding.AwayFromZero),
        SubscriptionCycle.Yearly => Math.Round(MedianAmount / 12m, 2, MidpointRounding.AwayFromZero),
        SubscriptionCycle.Quarterly => Math.Round(MedianAmount / 3m, 2, MidpointRounding.AwayFromZero),
        _ => MedianAmount
    };
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

            var cycle = InferCycle(group.Select(r => r.DateUtc).OrderBy(d => d).ToList());
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
                MedianAmount = Math.Round(median, 2, MidpointRounding.AwayFromZero),
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

    private static SubscriptionCycle InferCycle(List<DateTime> ordered)
    {
        if (ordered.Count < 2)
        {
            return SubscriptionCycle.Monthly;
        }

        var gaps = new List<double>();
        for (var i = 1; i < ordered.Count; i++)
        {
            gaps.Add((ordered[i] - ordered[i - 1]).TotalDays);
        }

        var avg = gaps.Average();
        if (avg is >= 5 and <= 10)
        {
            return SubscriptionCycle.Weekly;
        }

        if (avg is >= 80 and <= 100)
        {
            return SubscriptionCycle.Quarterly;
        }

        if (avg >= 300)
        {
            return SubscriptionCycle.Yearly;
        }

        return SubscriptionCycle.Monthly;
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
