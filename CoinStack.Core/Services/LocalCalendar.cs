namespace CoinStack.Services;

/// <summary>
/// Dates as the person sees them. Times are stored in UTC, but "today", a day's totals and the edges of
/// a pay period follow the device's time zone, so a coffee bought at 01:00 in Johannesburg counts on
/// that day and not the day before. The app always runs on the person's own device, so its time zone
/// is theirs; <see cref="TimeProvider.LocalTimeZone"/> lets tests pick another.
/// </summary>
public static class LocalCalendar
{
    /// <summary>Today's date on the device's calendar.</summary>
    public static DateTime Today(this TimeProvider clock) => clock.GetLocalNow().Date;

    /// <summary>The local calendar date a stored UTC time falls on.</summary>
    public static DateTime LocalDate(this TimeProvider clock, DateTime utc) => ToLocal(clock.LocalTimeZone, utc).Date;

    /// <summary>The UTC time at which a local calendar date begins.</summary>
    public static DateTime StartOfDayUtc(this TimeProvider clock, DateTime localDate) => StartOfDayUtc(clock.LocalTimeZone, localDate);

    public static DateTime ToLocal(TimeZoneInfo zone, DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);

    public static DateTime StartOfDayUtc(TimeZoneInfo zone, DateTime localDate)
    {
        var midnight = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);

        // Where the clocks jump forward at midnight, the day starts at its first real minute.
        while (zone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddMinutes(15);
        }

        return TimeZoneInfo.ConvertTimeToUtc(midnight, zone);
    }
}
