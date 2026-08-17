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
        var nowUtc = DateTime.UtcNow;
        var currentMonthKey = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // 1. Identify and flag pass-through transaction pairs (e.g., 50k in / 47k out to Pa on 07 Apr)
        var passThroughIds = DetectPassThroughIds(transactions);

        // 2. Build month-by-month history
        var byMonth = transactions
            .GroupBy(t => new DateTime(t.OccurredAtUtc.Year, t.OccurredAtUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var monthGroup in byMonth)
        {
            var income = monthGroup.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            var expenses = monthGroup.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

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

        // 4. Completed months vs ongoing partial month
        var completedMonthGroups = byMonth
            .Where(g => g.Key < currentMonthKey)
            .ToList();

        var validIncomeMonths = new List<decimal>();
        var validExpenseMonths = new List<decimal>();

        foreach (var monthGroup in completedMonthGroups)
        {
            var nonPassThrough = monthGroup.Where(t => !passThroughIds.Contains(t.Id)).ToList();
            var monthIncome = nonPassThrough.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            var monthExpense = nonPassThrough.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

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

            // If there's regular freelance / other non-salary income, add the median extra
            var otherIncomes = completedMonthGroups
                .Select(g => g.Where(t => t.Type == TransactionType.Income && !salaryTransactions.Contains(t) && !passThroughIds.Contains(t.Id)).Sum(t => t.Amount))
                .Where(amt => amt > 100m)
                .ToList();

            if (otherIncomes.Count >= 2)
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
            // Only current/single month exists. Prorate expenses to full-month equivalent.
            var singleGroup = byMonth[0];
            var nonPassThrough = singleGroup.Where(t => !passThroughIds.Contains(t.Id)).ToList();
            var singleExpense = nonPassThrough.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
            var singleIncome = nonPassThrough.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);

            var daysInMonth = DateTime.DaysInMonth(singleGroup.Key.Year, singleGroup.Key.Month);
            var daysElapsed = Math.Clamp(nowUtc.Day, 1, daysInMonth);

            var proratedExpense = daysElapsed > 0 && daysElapsed < daysInMonth
                ? (singleExpense / daysElapsed) * daysInMonth
                : singleExpense;

            projection.ProjectedMonthlyExpenses = RoundCurrency(proratedExpense);
            if (projection.ProjectedMonthlyIncome <= 0)
            {
                projection.ProjectedMonthlyIncome = singleIncome > 0 ? RoundCurrency(singleIncome) : (settings?.MonthlyIncome ?? 0m);
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
            .Where(t => t.Type == TransactionType.Income)
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
