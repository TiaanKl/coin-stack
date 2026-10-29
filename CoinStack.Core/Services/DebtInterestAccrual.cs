using CoinStack.Data.Entities;

namespace CoinStack.Services;

/// <summary>
/// Charges monthly interest onto a debt's stored balance so it tracks what the lender actually holds.
/// Without this, logging payments only ever subtracts and a debt shows as paid off months early.
/// </summary>
/// <remarks>
/// Interest posts once a month on the debit-order day (the day of <see cref="DebtAccount.PaymentStartDateUtc"/>,
/// clamped to short months) at <c>balance × annual rate / 12</c>, rounded to the cent.
/// <see cref="DebtAccount.InterestAccruedThroughUtc"/> records the last posting, so running this
/// repeatedly is idempotent. A debt with no marker starts accruing from today; nothing is back-charged.
/// </remarks>
public static class DebtInterestAccrual
{
    /// <summary>Safety cap so a corrupt marker can't spin for centuries.</summary>
    private const int MaxPostings = 1200;

    /// <summary>
    /// Posts every interest charge due up to <paramref name="utcNow"/> and returns the total added.
    /// </summary>
    public static decimal Accrue(DebtAccount debt, DateTime utcNow)
    {
        var today = utcNow.Date;

        if (debt.InterestAccruedThroughUtc is null)
        {
            debt.InterestAccruedThroughUtc = today;
            return 0m;
        }

        var anchorDay = debt.PaymentStartDateUtc.Day;
        var monthlyRate = debt.InterestRatePercent > 0m
            ? FinancialEngine.PercentToFraction(debt.InterestRatePercent) / 12m
            : 0m;

        var marker = debt.InterestAccruedThroughUtc.Value.Date;
        var total = 0m;

        for (var i = 0; i < MaxPostings; i++)
        {
            var next = NextPostingDate(marker, anchorDay);
            if (next > today)
            {
                break;
            }

            if (monthlyRate > 0m && debt.CurrentBalance > 0m)
            {
                var interest = FinancialEngine.RoundCurrency(debt.CurrentBalance * monthlyRate);
                debt.CurrentBalance += interest;
                total += interest;
            }

            marker = next;
        }

        debt.InterestAccruedThroughUtc = marker;
        return total;
    }

    /// <summary>The first posting date strictly after <paramref name="after"/>, on the anchor day of a month.</summary>
    internal static DateTime NextPostingDate(DateTime after, int anchorDay)
    {
        var candidate = OnDay(after.Year, after.Month, anchorDay);
        if (candidate <= after)
        {
            var nextMonth = new DateTime(after.Year, after.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
            candidate = OnDay(nextMonth.Year, nextMonth.Month, anchorDay);
        }

        return candidate;
    }

    private static DateTime OnDay(int year, int month, int day)
        => new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)), 0, 0, 0, DateTimeKind.Utc);
}
