using CoinStack.Data.Entities;
using CoinStack.Services.Import;

namespace CoinStack.Services;

/// <summary>One day of the forecast: running totals from tomorrow up to and including <paramref name="Date"/>.</summary>
public sealed record ForecastDay(DateTime Date, decimal IncomeToDate, decimal ExpensesToDate, decimal Balance);

public sealed class CashFlowForecast
{
    public decimal StartingBalance { get; init; }

    /// <summary>The month the forecast is built on: the same figures Bank Import shows as "Projected".</summary>
    public decimal MonthlyIncome { get; init; }

    public decimal MonthlyExpenses { get; init; }

    public List<ForecastDay> Days { get; } = [];

    public decimal Income => Days.Count == 0 ? 0m : Days[^1].IncomeToDate;

    public decimal Expenses => Days.Count == 0 ? 0m : Days[^1].ExpensesToDate;

    public decimal EndingBalance => Days.Count == 0 ? StartingBalance : Days[^1].Balance;
}

public interface IForecastService
{
    /// <summary>
    /// The balance day by day from tomorrow, starting at <paramref name="startingBalance"/>. Salary lands on
    /// payday and each subscription on its debit day; the rest of the month's expected income and spending
    /// is spread over the days, with more spending on the weekdays that usually cost more.
    /// </summary>
    Task<CashFlowForecast> ForecastAsync(decimal startingBalance, int days = 30, CancellationToken cancellationToken = default);
}

/// <summary>
/// The one forecast. Its monthly income and spending come from <see cref="IProjectionService"/>, the
/// engine behind Bank Import, so Home's 30 days and Bank Import's projected month always agree.
/// </summary>
public sealed class ForecastService : IForecastService
{
    /// <summary>The average month, so a month's spending spread per day doesn't depend on which month it is.</summary>
    internal const decimal DaysPerMonth = 365.25m / 12m;

    private const int PatternDays = 90;

    private readonly IProjectionService _projection;
    private readonly ISettingsService _settings;
    private readonly ISubscriptionService _subscriptions;
    private readonly ITransactionService _transactions;
    private readonly TimeProvider _clock;

    public ForecastService(
        IProjectionService projection,
        ISettingsService settings,
        ISubscriptionService subscriptions,
        ITransactionService transactions,
        TimeProvider? clock = null)
    {
        _projection = projection;
        _settings = settings;
        _subscriptions = subscriptions;
        _transactions = transactions;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<CashFlowForecast> ForecastAsync(decimal startingBalance, int days = 30, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        var projection = await _projection.BuildProjectionAsync(cancellationToken);
        var subscriptions = (await _subscriptions.GetAllAsync(cancellationToken))
            .Where(s => s.Status == SubscriptionStatus.Active && s.Cost > 0)
            .ToList();
        var subscriptionsPerMonth = subscriptions.Sum(SubscriptionCosts.MonthlyEquivalent);

        // Before there is any history, the month is what the settings say: salary in, debit orders out.
        var monthlyIncome = projection.MonthsOfData > 0 ? projection.ProjectedMonthlyIncome : Math.Max(0m, settings.MonthlyIncome);
        var monthlyExpenses = projection.MonthsOfData > 0 ? projection.ProjectedMonthlyExpenses : subscriptionsPerMonth;

        var salary = Math.Max(0m, settings.MonthlyIncome) > 0 ? Math.Min(settings.MonthlyIncome, monthlyIncome) : monthlyIncome;
        var otherIncomePerDay = Math.Max(0m, monthlyIncome - salary) / DaysPerMonth;
        var otherSpendingPerDay = Math.Max(0m, monthlyExpenses - subscriptionsPerMonth) / DaysPerMonth;

        var today = _clock.Today();
        var weekdayWeights = await WeekdayWeightsAsync(today, cancellationToken);

        var forecast = new CashFlowForecast
        {
            StartingBalance = startingBalance,
            MonthlyIncome = monthlyIncome,
            MonthlyExpenses = monthlyExpenses
        };

        decimal income = 0m, expenses = 0m;
        for (var day = 1; day <= days; day++)
        {
            var date = today.AddDays(day);
            var payday = BudgetPeriod.PaydayIn(date.Year, date.Month, settings.MonthStartDay);

            income += otherIncomePerDay + (date == payday ? salary : 0m);
            expenses += otherSpendingPerDay * weekdayWeights[date.DayOfWeek]
                        + subscriptions.Sum(s => SubscriptionCosts.ForecastChargeOn(s, date));

            forecast.Days.Add(new ForecastDay(
                date,
                Math.Round(income, 2),
                Math.Round(expenses, 2),
                Math.Round(startingBalance + income - expenses, 2)));
        }

        return forecast;
    }

    /// <summary>
    /// How much each weekday's spending differs from the average day over the last 90 days (1 = average).
    /// Days with nothing recorded count too, so a quiet Sunday stays quiet. Without history every day is 1.
    /// </summary>
    private async Task<Dictionary<DayOfWeek, decimal>> WeekdayWeightsAsync(DateTime today, CancellationToken cancellationToken)
    {
        var weights = Enum.GetValues<DayOfWeek>().ToDictionary(d => d, _ => 1m);

        var from = today.AddDays(-PatternDays);
        var spending = (await _transactions.GetFromDateAsync(_clock.StartOfDayUtc(from), cancellationToken))
            .Where(t => t.Type == TransactionType.Expense && t.SubscriptionId == null)
            .Select(t => (Day: _clock.LocalDate(t.OccurredAtUtc), t.Amount))
            .Where(t => t.Day >= from && t.Day < today)
            .ToList();

        if (spending.Count == 0)
        {
            return weights;
        }

        // Only the days since spending was first recorded, so a short history isn't diluted with empty days.
        var first = spending.Min(t => t.Day);
        var calendar = Enumerable.Range(0, (today - first).Days).Select(i => first.AddDays(i)).ToList();
        var averageDay = spending.Sum(t => t.Amount) / calendar.Count;
        if (averageDay <= 0m)
        {
            return weights;
        }

        foreach (var weekday in weights.Keys.ToList())
        {
            var dates = calendar.Count(d => d.DayOfWeek == weekday);
            if (dates > 0)
            {
                weights[weekday] = spending.Where(t => t.Day.DayOfWeek == weekday).Sum(t => t.Amount) / dates / averageDay;
            }
        }

        return weights;
    }
}
