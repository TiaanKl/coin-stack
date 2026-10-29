using System;

namespace CoinStack.Desktop.ViewModels;

internal static class BudgetPeriodHelper
{
    public static (DateTime StartUtc, DateTime EndUtc) GetBounds(int monthStartDay, DateTime utcNow)
    {
        if (monthStartDay is < 1 or > 28)
            monthStartDay = 1;

        var startThisMonth = new DateTime(utcNow.Year, utcNow.Month, monthStartDay, 0, 0, 0, DateTimeKind.Utc);
        var startUtc = utcNow.Day >= monthStartDay ? startThisMonth : startThisMonth.AddMonths(-1);
        var endUtc = startUtc.AddMonths(1);
        return (startUtc, endUtc);
    }
}
