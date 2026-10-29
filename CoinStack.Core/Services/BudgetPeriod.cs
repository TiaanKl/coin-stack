namespace CoinStack.Services;

/// <summary>
/// A budget period runs from one payday up to, but not including, the next. Payday can be any day
/// from 1 to 31; in a month without that day it falls on the month's last day, so payday 31 is
/// 30 April and 28 February. Every page and service uses this one calculation so their totals agree.
/// The edges are midnight on the device's calendar, stored in UTC like every other time.
/// </summary>
public readonly record struct BudgetPeriod(int Payday, DateTime StartUtc, DateTime EndUtc)
{
    public const int LastPayday = 31;

    private readonly TimeZoneInfo? _zone;

    /// <summary>The time zone whose midnights start and end the period.</summary>
    public TimeZoneInfo Zone
    {
        get => _zone ?? TimeZoneInfo.Utc;
        init => _zone = value;
    }

    /// <summary>The first day inside the period on the local calendar.</summary>
    public DateTime FirstDay => LocalCalendar.ToLocal(Zone, StartUtc).Date;

    /// <summary>The last day inside the period, for labels such as "25 Oct – 24 Nov".</summary>
    public DateTime LastDay => LocalCalendar.ToLocal(Zone, EndUtc).Date.AddDays(-1);

    public BudgetPeriod Previous => Containing(Payday, StartUtc.AddTicks(-1), Zone);

    public BudgetPeriod Next => Containing(Payday, EndUtc, Zone);

    /// <summary>The period that <paramref name="utcNow"/> falls in on the device's calendar.</summary>
    public static BudgetPeriod Containing(int payday, DateTime utcNow) => Containing(payday, utcNow, TimeZoneInfo.Local);

    public static BudgetPeriod Containing(int payday, DateTime utcNow, TimeZoneInfo zone)
    {
        payday = NormalizePayday(payday);
        var today = LocalCalendar.ToLocal(zone, utcNow).Date;
        var paydayThisMonth = PaydayIn(today.Year, today.Month, payday);

        DateTime first, end;
        if (today >= paydayThisMonth)
        {
            var next = paydayThisMonth.AddMonths(1);
            (first, end) = (paydayThisMonth, PaydayIn(next.Year, next.Month, payday));
        }
        else
        {
            var previous = paydayThisMonth.AddMonths(-1);
            (first, end) = (PaydayIn(previous.Year, previous.Month, payday), paydayThisMonth);
        }

        return new BudgetPeriod(payday, LocalCalendar.StartOfDayUtc(zone, first), LocalCalendar.StartOfDayUtc(zone, end))
        {
            Zone = zone
        };
    }

    /// <summary>The calendar date payday falls on in a given month, moved back to the last day when the month is shorter.</summary>
    public static DateTime PaydayIn(int year, int month, int payday)
    {
        var day = Math.Min(NormalizePayday(payday), DateTime.DaysInMonth(year, month));
        return new DateTime(year, month, day);
    }

    /// <summary>
    /// The payday that matches the dates salary arrived on, or 0 when there are none. Salary that always
    /// lands on the month's last day, including a 31st, is payday 31. Otherwise the most common day wins,
    /// and a tie goes to the earlier day so the salary never arrives before its period starts.
    /// </summary>
    public static int DetectPayday(IEnumerable<DateTime> salaryDates)
    {
        var dates = salaryDates.ToList();
        if (dates.Count == 0)
        {
            return 0;
        }

        if (dates.All(d => d.Day == DateTime.DaysInMonth(d.Year, d.Month)) && dates.Any(d => d.Day == LastPayday))
        {
            return LastPayday;
        }

        return dates
            .GroupBy(d => d.Day)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First()
            .Key;
    }

    /// <summary>Anything outside 1–31 (an unset or corrupt setting) means the 1st.</summary>
    public static int NormalizePayday(int payday) => payday is >= 1 and <= LastPayday ? payday : 1;

    public bool Contains(DateTime utc) => utc >= StartUtc && utc < EndUtc;
}
