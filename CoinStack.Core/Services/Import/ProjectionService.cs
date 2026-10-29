using System.Text.RegularExpressions;
using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services.Import;

public sealed record MonthlyActual(DateTime Month, decimal Income, decimal Expenses, decimal Net, int TransactionCount);

public sealed record RecurringItem(string Description, decimal AverageAmount, int MonthsSeen, string? CategoryName, TransactionType Type);

public sealed class TopMerchantSpendItem
{
    public string MerchantName { get; set; } = "";
    public string NormalizedKey { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string IconClass { get; set; } = "fa-solid fa-store";
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
    public decimal MonthlyAverage => MonthsSeen > 0 ? Math.Round(TotalAmount / MonthsSeen, 2) : TotalAmount;
    public int MonthsSeen { get; set; } = 1;
}

public sealed class CashFlowProjection
{
    public List<MonthlyActual> History { get; } = [];

    public decimal ProjectedMonthlyIncome { get; set; }
    public decimal ProjectedMonthlyExpenses { get; set; }
    public decimal ProjectedMonthlyNet { get; set; }
    public decimal SavingsRatePercent { get; set; }

    public List<RecurringItem> RecurringExpenses { get; } = [];
    public List<RecurringItem> RecurringIncome { get; } = [];

    public List<TopMerchantSpendItem> TopMerchants { get; } = [];

    public decimal RecurringExpenseTotal { get; set; }
    public decimal RecurringIncomeTotal { get; set; }

    public int MonthsOfData => History.Count;
}

public interface IProjectionService
{
    /// <summary>
    /// Builds a forward cash-flow projection from all recorded transactions
    /// (manual entries plus every imported statement).
    /// </summary>
    Task<CashFlowProjection> BuildProjectionAsync(CancellationToken cancellationToken = default);
}

public sealed partial class ProjectionService : IProjectionService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    [GeneratedRegex(@"(?:^|\s)\d+(?:[.,]\d+)*(?=\s|$)")]
    private static partial Regex PriceTokenRegex();

    public ProjectionService(IDbContextFactory<CoinStackDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<CashFlowProjection> BuildProjectionAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var transactions = await db.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.Type != TransactionType.Transfer)
            .ToListAsync(cancellationToken);

        transactions.RemoveAll(TransactionConventions.IsSyntheticMonthlyIncome);

        var projection = new CashFlowProjection();

        if (transactions.Count == 0)
        {
            return projection;
        }

        var settings = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var statementPeriods = await LoadStatementPeriodsAsync(db, cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var currentMonthKey = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // 1. Identify and flag pass-through transaction pairs (e.g., 50k in / 47k out to Pa on 07 Apr)
        var passThroughIds = DetectPassThroughIds(transactions);

        // Refunds give back money already spent: they reduce that month's spending instead of counting as income.
        var refundIds = transactions.Where(TransactionConventions.IsRefund).Select(t => t.Id).ToHashSet();

        decimal IncomeOf(IEnumerable<Transaction> txs) => txs
            .Where(t => t.Type == TransactionType.Income && !refundIds.Contains(t.Id))
            .Sum(t => t.Amount);

        decimal ExpensesOf(IEnumerable<Transaction> txs)
        {
            var list = txs as IReadOnlyCollection<Transaction> ?? txs.ToList();
            var spent = list.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
            var refunded = list.Where(t => refundIds.Contains(t.Id)).Sum(t => t.Amount);
            return Math.Max(0m, spent - refunded);
        }

        // 2. Build month-by-month history
        var byMonth = transactions
            .GroupBy(t => new DateTime(t.OccurredAtUtc.Year, t.OccurredAtUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var monthGroup in byMonth)
        {
            var income = IncomeOf(monthGroup);
            var expenses = ExpensesOf(monthGroup);

            projection.History.Add(new MonthlyActual(
                monthGroup.Key,
                RoundCurrency(income),
                RoundCurrency(expenses),
                RoundCurrency(income - expenses),
                monthGroup.Count()));
        }

        // 3. Detect regular salary payments
        var salaryTransactions = transactions
            .Where(t => t.Type == TransactionType.Income
                        && (TransactionConventions.LooksLikeSalary(t.Description)
                            || (t.Notes is not null && TransactionConventions.LooksLikeSalary(t.Notes))))
            .ToList();

        var detectedSalary = salaryTransactions.Count > 0
            ? Math.Round(salaryTransactions.Average(t => t.Amount), 2, MidpointRounding.AwayFromZero)
            : 0m;

        // 4. Completed months vs ongoing partial month. A finished month that a bank statement only
        //    partly covers (a statement starting on the 18th) is partial too: its totals are too low.
        var firstDataDay = transactions.Min(t => t.OccurredAtUtc).Date;
        var coveredDaysByMonth = byMonth.ToDictionary(
            g => g.Key,
            g => CountCoveredDays(g.Key, g, statementPeriods, firstDataDay, nowUtc.Date));

        var completeMonthGroups = byMonth
            .Where(g => g.Key < currentMonthKey
                        && coveredDaysByMonth[g.Key] >= DateTime.DaysInMonth(g.Key.Year, g.Key.Month) - CoverageToleranceDays)
            .ToList();

        var validIncomeMonths = new List<decimal>();
        var validExpenseMonths = new List<decimal>();

        foreach (var monthGroup in completeMonthGroups)
        {
            var nonPassThrough = monthGroup.Where(t => !passThroughIds.Contains(t.Id)).ToList();
            var monthIncome = IncomeOf(nonPassThrough);
            var monthExpense = ExpensesOf(nonPassThrough);

            // If salary is detected, ignore statement cutoff months that have 0 income because statement ended before payday
            if (detectedSalary > 0)
            {
                if (monthIncome >= detectedSalary * 0.5m)
                {
                    validIncomeMonths.Add(monthIncome);
                }
            }
            else if (monthIncome > 0)
            {
                validIncomeMonths.Add(monthIncome);
            }

            if (monthExpense > 0)
            {
                validExpenseMonths.Add(monthExpense);
            }
        }

        // 5. Calculate Projected Income
        if (detectedSalary > 0)
        {
            // Lock in true detected salary as the primary income anchor
            projection.ProjectedMonthlyIncome = detectedSalary;

            // Other income (freelance, side jobs) counts at its median across every complete month,
            // months without any included, so a once-off payment isn't assumed to arrive every month.
            var otherIncomes = completeMonthGroups
                .Select(g => IncomeOf(g.Where(t => !salaryTransactions.Contains(t) && !passThroughIds.Contains(t.Id))))
                .ToList();

            if (otherIncomes.Count > 0)
            {
                projection.ProjectedMonthlyIncome += RoundCurrency(Median(otherIncomes));
            }
        }
        else if (validIncomeMonths.Count > 0)
        {
            projection.ProjectedMonthlyIncome = RoundCurrency(Median(validIncomeMonths));
        }
        else if (settings is not null && settings.MonthlyIncome > 0)
        {
            projection.ProjectedMonthlyIncome = settings.MonthlyIncome;
        }

        // 6. Calculate Projected Expenses
        if (validExpenseMonths.Count > 0)
        {
            projection.ProjectedMonthlyExpenses = RoundCurrency(Median(validExpenseMonths));
        }
        else if (byMonth.Count > 0)
        {
            // No complete month yet: scale up the best-covered partial month. Only day-to-day spending
            // is scaled; rent, debit orders and other large payments happen once a month, so R8 000 rent
            // paid on the 3rd stays R8 000 instead of becoming R80 000+.
            var partial = byMonth
                .OrderByDescending(g => coveredDaysByMonth[g.Key])
                .ThenByDescending(g => g.Key)
                .First();

            var nonPassThrough = partial.Where(t => !passThroughIds.Contains(t.Id)).ToList();
            var partialIncome = IncomeOf(nonPassThrough);

            var incomeForThreshold = projection.ProjectedMonthlyIncome > 0
                ? projection.ProjectedMonthlyIncome
                : settings?.MonthlyIncome ?? 0m;

            projection.ProjectedMonthlyExpenses = RoundCurrency(EstimateFullMonthExpenses(
                nonPassThrough,
                refundIds,
                coveredDaysByMonth[partial.Key],
                DateTime.DaysInMonth(partial.Key.Year, partial.Key.Month),
                incomeForThreshold));

            if (projection.ProjectedMonthlyIncome <= 0)
            {
                projection.ProjectedMonthlyIncome = partialIncome > 0 ? RoundCurrency(partialIncome) : (settings?.MonthlyIncome ?? 0m);
            }
        }

        // Fallback to configured monthly income if transactions show 0 projected income
        if (projection.ProjectedMonthlyIncome <= 0 && settings is not null && settings.MonthlyIncome > 0)
        {
            projection.ProjectedMonthlyIncome = settings.MonthlyIncome;
        }

        projection.ProjectedMonthlyNet = projection.ProjectedMonthlyIncome - projection.ProjectedMonthlyExpenses;

        projection.SavingsRatePercent = projection.ProjectedMonthlyIncome > 0
            ? Math.Round(projection.ProjectedMonthlyNet / projection.ProjectedMonthlyIncome * 100m, 1)
            : 0m;

        // 7. Detect recurring items & build top merchant spends
        DetectRecurring(transactions.Where(t => !passThroughIds.Contains(t.Id)).ToList(), byMonth.Count, projection);
        BuildTopMerchants(transactions.Where(t => !passThroughIds.Contains(t.Id)).ToList(), byMonth.Count, projection);

        return projection;
    }

    /// <summary>A finished month missing at most this many days of statement coverage still counts as complete.</summary>
    private const int CoverageToleranceDays = 2;

    /// <summary>
    /// The date ranges the imported statements cover: the printed statement period, or the first to
    /// last transaction date when the file had none.
    /// </summary>
    private static async Task<List<(DateTime Start, DateTime End)>> LoadStatementPeriodsAsync(
        CoinStackDbContext db,
        CancellationToken cancellationToken)
    {
        var imports = await db.StatementImports
            .AsNoTracking()
            .Where(i => i.Status == StatementImportStatus.Imported)
            .Select(i => new { i.Id, i.PeriodStartUtc, i.PeriodEndUtc })
            .ToListAsync(cancellationToken);

        var dateRanges = await db.Transactions
            .AsNoTracking()
            .Where(t => t.StatementImportId != null)
            .Select(t => new { ImportId = t.StatementImportId!.Value, t.OccurredAtUtc })
            .ToListAsync(cancellationToken);

        var rangeByImport = dateRanges
            .GroupBy(r => r.ImportId)
            .ToDictionary(g => g.Key, g => (First: g.Min(r => r.OccurredAtUtc), Last: g.Max(r => r.OccurredAtUtc)));

        var periods = new List<(DateTime Start, DateTime End)>();
        foreach (var import in imports)
        {
            rangeByImport.TryGetValue(import.Id, out var range);
            var start = import.PeriodStartUtc ?? (range.First == default ? (DateTime?)null : range.First);
            var end = import.PeriodEndUtc ?? (range.Last == default ? (DateTime?)null : range.Last);
            if (start is not null && end is not null && end >= start)
            {
                periods.Add((start.Value.Date, end.Value.Date));
            }
        }

        return periods;
    }

    /// <summary>
    /// How many days of <paramref name="monthKey"/>'s month the data covers. Months of imported
    /// statements count the days inside a statement period; a finished month of manual entries counts
    /// as fully covered; the current month runs from the 1st (or the first entry) up to today.
    /// </summary>
    private static int CountCoveredDays(
        DateTime monthKey,
        IEnumerable<Transaction> monthTransactions,
        List<(DateTime Start, DateTime End)> statementPeriods,
        DateTime firstDataDay,
        DateTime today)
    {
        var daysInMonth = DateTime.DaysInMonth(monthKey.Year, monthKey.Month);
        var monthStart = monthKey.Date;
        var monthEnd = monthStart.AddDays(daysInMonth - 1);
        var lastDay = monthEnd < today ? monthEnd : today;
        if (lastDay < monthStart)
        {
            lastDay = monthEnd;
        }

        var fromStatements = statementPeriods.Count > 0 && monthTransactions.Any(t => t.StatementImportId is not null);
        if (fromStatements)
        {
            var covered = 0;
            for (var day = monthStart; day <= lastDay; day = day.AddDays(1))
            {
                if (statementPeriods.Any(p => day >= p.Start && day <= p.End))
                {
                    covered++;
                }
            }

            return covered;
        }

        if (monthEnd < today)
        {
            return daysInMonth;
        }

        var firstDay = firstDataDay > monthStart ? firstDataDay : monthStart;
        return Math.Max(1, (lastDay - firstDay).Days + 1);
    }

    /// <summary>
    /// Scales a partly covered month up to a full month. Day-to-day spending grows with the number of
    /// days; payments that happen once a month (debit orders, debt and subscription payments, mandatory
    /// expenses and anything over 5% of income) are counted once.
    /// </summary>
    private static decimal EstimateFullMonthExpenses(
        IReadOnlyCollection<Transaction> transactions,
        HashSet<int> refundIds,
        int coveredDays,
        int daysInMonth,
        decimal monthlyIncome)
    {
        var lumpThreshold = monthlyIncome > 0m ? monthlyIncome * 0.05m : 1000m;

        var oncePerMonth = 0m;
        var dayToDay = 0m;
        foreach (var tx in transactions.Where(t => t.Type == TransactionType.Expense))
        {
            var isOncePerMonth = tx.ExpenseKind == ExpenseKind.Mandatory
                                 || tx.AutoDeduct
                                 || tx.SubscriptionId is not null
                                 || tx.DebtAccountId is not null
                                 || tx.Amount >= lumpThreshold;

            if (isOncePerMonth)
            {
                oncePerMonth += tx.Amount;
            }
            else
            {
                dayToDay += tx.Amount;
            }
        }

        var refunded = transactions.Where(t => refundIds.Contains(t.Id)).Sum(t => t.Amount);
        var scale = coveredDays >= daysInMonth ? 1m : (decimal)daysInMonth / Math.Max(1, coveredDays);

        return Math.Max(0m, oncePerMonth + (dayToDay * scale) - refunded);
    }

    private static HashSet<int> DetectPassThroughIds(List<Transaction> transactions)
    {
        var passThroughIds = new HashSet<int>();

        var largeCredits = transactions
            .Where(t => t.Type == TransactionType.Income && t.Amount >= 5000m && !TransactionConventions.LooksLikeSalary(t.Description))
            .ToList();

        foreach (var credit in largeCredits)
        {
            var matchingDebit = transactions.FirstOrDefault(d =>
                d.Type == TransactionType.Expense
                && Math.Abs((d.OccurredAtUtc - credit.OccurredAtUtc).TotalDays) <= 2
                && d.Amount >= credit.Amount * 0.8m
                && d.Amount <= credit.Amount * 1.1m);

            if (matchingDebit is not null)
            {
                passThroughIds.Add(credit.Id);
                passThroughIds.Add(matchingDebit.Id);
            }
        }

        return passThroughIds;
    }

    private static void BuildTopMerchants(List<Transaction> transactions, int monthCount, CashFlowProjection projection)
    {
        var expenseTransactions = transactions
            .Where(t => t.Type == TransactionType.Expense)
            .ToList();

        var groups = expenseTransactions
            .GroupBy(t => MerchantNormalizer.Normalize(t.Description, TransactionType.Expense).NormalizedKey)
            .ToList();

        foreach (var group in groups)
        {
            var sample = group.First();
            var info = MerchantNormalizer.Normalize(sample.Description, TransactionType.Expense);
            var monthsSeen = group.Select(t => new DateTime(t.OccurredAtUtc.Year, t.OccurredAtUtc.Month, 1)).Distinct().Count();

            projection.TopMerchants.Add(new TopMerchantSpendItem
            {
                MerchantName = info.CanonicalName,
                NormalizedKey = info.NormalizedKey,
                CategoryName = sample.Category?.Name ?? info.CategoryName,
                IconClass = info.IconClass,
                TotalAmount = RoundCurrency(group.Sum(t => t.Amount)),
                TransactionCount = group.Count(),
                MonthsSeen = Math.Max(1, monthsSeen)
            });
        }

        projection.TopMerchants.Sort((a, b) => b.TotalAmount.CompareTo(a.TotalAmount));
    }

    private static void DetectRecurring(List<Transaction> transactions, int monthCount, CashFlowProjection projection)
    {
        var minMonths = monthCount >= 3 ? 2 : 1;

        var expenseGroups = transactions
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => NormalizeForRecurring(t.Description));

        foreach (var group in expenseGroups)
        {
            if (string.IsNullOrWhiteSpace(group.Key))
            {
                continue;
            }

            var monthsSeen = group.Select(t => new DateTime(t.OccurredAtUtc.Year, t.OccurredAtUtc.Month, 1)).Distinct().Count();
            if (monthsSeen < minMonths)
            {
                continue;
            }

            var amounts = group.Select(t => t.Amount).ToList();
            var average = amounts.Average();
            var maxDeviation = amounts.Max(a => Math.Abs(a - average));

            if (amounts.Count > 1 && average > 0 && maxDeviation / average > 0.15m)
            {
                continue;
            }

            var first = group.OrderByDescending(t => t.OccurredAtUtc).First();

            projection.RecurringExpenses.Add(new RecurringItem(
                first.Description,
                RoundCurrency(average),
                monthsSeen,
                first.Category?.Name,
                TransactionType.Expense));
        }

        var incomeGroups = transactions
            .Where(t => t.Type == TransactionType.Income && !TransactionConventions.IsRefund(t))
            .GroupBy(t => NormalizeForRecurring(t.Description));

        foreach (var group in incomeGroups)
        {
            if (string.IsNullOrWhiteSpace(group.Key))
            {
                continue;
            }

            var monthsSeen = group.Select(t => new DateTime(t.OccurredAtUtc.Year, t.OccurredAtUtc.Month, 1)).Distinct().Count();
            if (monthsSeen < minMonths)
            {
                continue;
            }

            var amounts = group.Select(t => t.Amount).ToList();
            var average = amounts.Average();
            var maxDeviation = amounts.Max(a => Math.Abs(a - average));

            if (amounts.Count > 1 && average > 0 && maxDeviation / average > 0.15m)
            {
                continue;
            }

            var first = group.OrderByDescending(t => t.OccurredAtUtc).First();

            projection.RecurringIncome.Add(new RecurringItem(
                first.Description,
                RoundCurrency(average),
                monthsSeen,
                first.Category?.Name,
                TransactionType.Income));
        }

        projection.RecurringExpenseTotal = RoundCurrency(projection.RecurringExpenses.Sum(r => r.AverageAmount));
        projection.RecurringIncomeTotal = RoundCurrency(projection.RecurringIncome.Sum(r => r.AverageAmount));

        projection.RecurringExpenses.Sort((a, b) => b.AverageAmount.CompareTo(a.AverageAmount));
        projection.RecurringIncome.Sort((a, b) => b.AverageAmount.CompareTo(a.AverageAmount));
    }

    private static string NormalizeForRecurring(string description)
    {
        var normalized = PriceTokenRegex().Replace(description, " ").ToUpperInvariant();
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private static decimal Median(IEnumerable<decimal> values)
    {
        var list = values.OrderBy(v => v).ToList();
        if (list.Count == 0)
        {
            return 0m;
        }

        var mid = list.Count / 2;
        return list.Count % 2 == 0
            ? (list[mid - 1] + list[mid]) / 2m
            : list[mid];
    }

    private static decimal RoundCurrency(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
