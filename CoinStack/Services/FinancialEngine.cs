namespace CoinStack.Services;

/// <summary>
/// Immutable breakdown of a fixed-instalment (amortising) loan.
/// All monetary members are already rounded to 2 decimal places using commercial rounding.
/// </summary>
/// <param name="MonthlyPayment">The fixed instalment due every month.</param>
/// <param name="TotalPayment">The sum of every instalment across the full term.</param>
/// <param name="TotalInterest">The total finance charge (<see cref="TotalPayment"/> minus principal).</param>
/// <param name="Principal">The original amount borrowed.</param>
/// <param name="TermMonths">The number of monthly instalments.</param>
/// <param name="AnnualInterestRate">The nominal annual rate expressed as a fraction (0.075m = 7.5%).</param>
public readonly record struct LoanPaymentBreakdown(
    decimal MonthlyPayment,
    decimal TotalPayment,
    decimal TotalInterest,
    decimal Principal,
    int TermMonths,
    decimal AnnualInterestRate);

/// <summary>
/// Immutable result of a savings/annuity projection.
/// All monetary members are already rounded to 2 decimal places using commercial rounding.
/// </summary>
/// <param name="FutureValue">The projected closing balance at the end of the horizon.</param>
/// <param name="TotalContributions">Initial balance plus every recurring deposit made.</param>
/// <param name="TotalInterestEarned">The growth attributable purely to interest.</param>
/// <param name="Months">The projection horizon in months.</param>
public readonly record struct SavingsProjection(
    decimal FutureValue,
    decimal TotalContributions,
    decimal TotalInterestEarned,
    int Months);

/// <summary>
/// A single instalment row produced by <see cref="FinancialEngine.BuildAmortisationSchedule"/>.
/// </summary>
/// <param name="PaymentNumber">The one-based instalment index.</param>
/// <param name="OpeningBalance">The balance owing before this instalment.</param>
/// <param name="Payment">The amount actually paid this month (the final row may be smaller).</param>
/// <param name="InterestPortion">The share of the payment consumed by interest.</param>
/// <param name="PrincipalPortion">The share of the payment that reduces the balance.</param>
/// <param name="ClosingBalance">The balance owing after this instalment.</param>
public readonly record struct AmortisationInstalment(
    int PaymentNumber,
    decimal OpeningBalance,
    decimal Payment,
    decimal InterestPortion,
    decimal PrincipalPortion,
    decimal ClosingBalance);

/// <summary>
/// The outcome of amortising a balance at a fixed monthly payment.
/// </summary>
/// <param name="Instalments">The month-by-month instalment rows.</param>
/// <param name="MonthsToPayoff">The number of instalments required, or <see langword="null"/> when the debt never clears.</param>
/// <param name="TotalPaid">The sum of every instalment.</param>
/// <param name="TotalInterest">The total interest paid across the schedule.</param>
/// <param name="IsUnpayable">
/// <see langword="true"/> when the payment cannot clear the balance because it never exceeds the monthly interest.
/// </param>
public sealed record AmortisationSchedule(
    IReadOnlyList<AmortisationInstalment> Instalments,
    int? MonthsToPayoff,
    decimal TotalPaid,
    decimal TotalInterest,
    bool IsUnpayable);

/// <summary>
/// Deterministic, side-effect free, thread-safe financial mathematics engine.
/// </summary>
/// <remarks>
/// <para>
/// Every calculation is performed exclusively with <see cref="decimal"/> arithmetic. No
/// <see cref="double"/> or <see cref="float"/> value ever participates in a monetary code path,
/// which removes the binary-representation drift that plagues <see cref="Math.Pow(double, double)"/>
/// based implementations.
/// </para>
/// <para>
/// Interest and inflation rates are always supplied as fractions, not percentages:
/// <c>0.075m</c> means 7.5%. Final currency values are rounded to 2 decimals with
/// <see cref="MidpointRounding.AwayFromZero"/> (standard commercial rounding).
/// </para>
/// <para>
/// The class holds no mutable state, so all members are safe to call concurrently from any thread.
/// </para>
/// </remarks>
public static class FinancialEngine
{
    /// <summary>Number of decimal places used for all monetary output.</summary>
    public const int CurrencyDecimals = 2;

    /// <summary>Upper sanity bound for a rate fraction (10 000%), used to reject percentage/fraction mix-ups.</summary>
    private const decimal MaxRate = 100m;

    /// <summary>Upper sanity bound for a projection horizon, guarding against runaway loops and overflow.</summary>
    private const int MaxPeriods = 100_000;

    /// <summary>Guards against a negative monetary amount.</summary>
    /// <param name="value">The amount to validate.</param>
    /// <param name="parameterName">The caller's parameter name, used in the exception message.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    private static void ValidatePrincipal(decimal value, string parameterName)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, value, $"{parameterName} must be zero or greater; negative amounts are not supported.");
        }
    }

    /// <summary>Guards against a negative or implausibly large rate fraction.</summary>
    /// <param name="rate">The rate fraction to validate.</param>
    /// <param name="parameterName">The caller's parameter name, used in the exception message.</param>
    /// <exception cref="ArgumentOutOfRangeException">The rate is negative or exceeds <see cref="MaxRate"/>.</exception>
    private static void ValidateRate(decimal rate, string parameterName)
    {
        if (rate < 0m)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, rate, $"{parameterName} must be zero or greater; negative rates are not supported.");
        }

        if (rate > MaxRate)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, rate,
                $"{parameterName} must be supplied as a fraction (0.075 for 7.5%), not a percentage. Values above {MaxRate} are rejected.");
        }
    }

    /// <summary>Guards against a negative duration expressed in years.</summary>
    /// <param name="years">The duration to validate.</param>
    /// <param name="parameterName">The caller's parameter name, used in the exception message.</param>
    /// <exception cref="ArgumentOutOfRangeException">The duration is negative.</exception>
    private static void ValidateDuration(decimal years, string parameterName)
    {
        if (years < 0m)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, years, $"{parameterName} must be zero or greater; time cannot run backwards.");
        }
    }

    /// <summary>Guards against a compounding frequency that is not strictly positive.</summary>
    /// <param name="periodsPerYear">The compounding frequency to validate.</param>
    /// <param name="parameterName">The caller's parameter name, used in the exception message.</param>
    /// <exception cref="ArgumentOutOfRangeException">The frequency is zero or negative.</exception>
    private static void ValidateCompoundingPeriods(int periodsPerYear, string parameterName)
    {
        if (periodsPerYear <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, periodsPerYear,
                $"{parameterName} must be greater than zero (1 = annually, 12 = monthly, 365 = daily).");
        }
    }

    /// <summary>Guards against a total period count large enough to overflow or stall the engine.</summary>
    /// <param name="totalPeriods">The computed number of compounding periods.</param>
    /// <exception cref="ArgumentOutOfRangeException">The period count exceeds <see cref="MaxPeriods"/>.</exception>
    private static void ValidatePeriodCount(decimal totalPeriods)
    {
        if (totalPeriods > MaxPeriods)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalPeriods), totalPeriods,
                $"The requested compounding frequency and duration produce {totalPeriods} periods, which exceeds the supported maximum of {MaxPeriods}.");
        }
    }

    /// <summary>
    /// Rounds a monetary amount to 2 decimal places using standard commercial rounding
    /// (<see cref="MidpointRounding.AwayFromZero"/>).
    /// </summary>
    /// <param name="value">The raw, unrounded amount.</param>
    /// <returns>The amount rounded to <see cref="CurrencyDecimals"/> decimal places.</returns>
    public static decimal RoundCurrency(decimal value) =>
        decimal.Round(value, CurrencyDecimals, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Amortises a balance at a fixed monthly payment, returning the full instalment schedule.
    /// </summary>
    /// <remarks>
    /// Interest is charged on the opening balance each month and rounded to the cent before the
    /// principal split, mirroring how lenders actually post interest. The final instalment is
    /// reduced to exactly settle the account, so the schedule never overshoots.
    /// </remarks>
    /// <param name="balance">The opening balance. Must be zero or greater.</param>
    /// <param name="annualRate">The annual nominal rate as a fraction (0.115m = 11.5%). Must be zero or greater.</param>
    /// <param name="monthlyPayment">The fixed monthly payment. Must be greater than zero.</param>
    /// <param name="maxMonths">A safety cap on the number of instalments generated. Must be greater than zero.</param>
    /// <returns>
    /// The completed schedule. When the payment never exceeds the monthly interest the result is
    /// flagged <see cref="AmortisationSchedule.IsUnpayable"/> with no instalments.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="balance"/> is negative, <paramref name="annualRate"/> is negative or implausibly large,
    /// <paramref name="monthlyPayment"/> is zero or negative, or <paramref name="maxMonths"/> is not positive.
    /// </exception>
    public static AmortisationSchedule BuildAmortisationSchedule(
        decimal balance,
        decimal annualRate,
        decimal monthlyPayment,
        int maxMonths = 600)
    {
        ValidatePrincipal(balance, nameof(balance));
        ValidateRate(annualRate, nameof(annualRate));

        if (monthlyPayment <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(monthlyPayment), monthlyPayment, "Monthly payment must be greater than zero.");
        }

        if (maxMonths <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxMonths), maxMonths, "The instalment cap must be greater than zero.");
        }

        if (balance == 0m)
        {
            return new AmortisationSchedule([], 0, 0m, 0m, false);
        }

        var monthlyRate = annualRate / 12m;

        // A payment that cannot cover the first month's interest never reduces the balance.
        if (monthlyRate > 0m && monthlyPayment <= RoundCurrency(balance * monthlyRate))
        {
            return new AmortisationSchedule([], null, 0m, 0m, true);
        }

        var instalments = new List<AmortisationInstalment>(Math.Min(maxMonths, 120));
        var remaining = RoundCurrency(balance);
        var totalPaid = 0m;
        var totalInterest = 0m;

        while (remaining > 0m && instalments.Count < maxMonths)
        {
            var openingBalance = remaining;
            var interest = monthlyRate > 0m ? RoundCurrency(openingBalance * monthlyRate) : 0m;

            // The closing instalment settles principal plus the final month's interest exactly.
            var payoffAmount = openingBalance + interest;
            var payment = monthlyPayment < payoffAmount ? monthlyPayment : payoffAmount;

            var principalPortion = payment - interest;
            var closingBalance = RoundCurrency(openingBalance - principalPortion);

            totalPaid += payment;
            totalInterest += interest;

            instalments.Add(new AmortisationInstalment(
                instalments.Count + 1,
                openingBalance,
                RoundCurrency(payment),
                interest,
                RoundCurrency(principalPortion),
                closingBalance));

            remaining = closingBalance;
        }

        var cleared = remaining <= 0m;

        return new AmortisationSchedule(
            instalments,
            cleared ? instalments.Count : null,
            RoundCurrency(totalPaid),
            RoundCurrency(totalInterest),
            !cleared);
    }

    /// <summary>
    /// Converts a human-facing percentage into the rate fraction the engine expects.
    /// </summary>
    /// <remarks>
    /// The UI and database store rates as percentages (<c>7.5</c> meaning 7.5%), whereas every
    /// <see cref="FinancialEngine"/> entry point takes a fraction (<c>0.075</c>). Routing conversions
    /// through this method keeps the boundary explicit instead of relying on magnitude guesswork.
    /// </remarks>
    /// <param name="percent">The rate as a percentage. Must be zero or greater.</param>
    /// <returns>The equivalent rate fraction.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="percent"/> is negative.</exception>
    public static decimal PercentToFraction(decimal percent)
    {
        if (percent < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percent), percent, "Percentage must be zero or greater.");
        }

        return percent / 100m;
    }

    /// <summary>
    /// Calculates the future value of a lump sum under compound interest:
    /// <c>A = P * (1 + r/n)^(n*t)</c>.
    /// </summary>
    /// <param name="principal">The initial deposit. Must be zero or greater.</param>
    /// <param name="annualRate">
    /// The nominal annual interest rate as a fraction (0.05m = 5%). Must be zero or greater.
    /// </param>
    /// <param name="compoundingPeriodsPerYear">
    /// Compounding events per year: 1 = annually, 4 = quarterly, 12 = monthly, 365 = daily. Must be greater than zero.
    /// </param>
    /// <param name="years">The investment horizon in years. Must be zero or greater.</param>
    /// <returns>The accrued amount, rounded to 2 decimal places.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="principal"/> is negative, <paramref name="annualRate"/> is negative or implausibly large,
    /// <paramref name="compoundingPeriodsPerYear"/> is zero or negative, or <paramref name="years"/> is negative.
    /// </exception>
    public static decimal CalculateCompoundInterest(
        decimal principal,
        decimal annualRate,
        int compoundingPeriodsPerYear,
        decimal years)
    {
        ValidatePrincipal(principal, nameof(principal));
        ValidateRate(annualRate, nameof(annualRate));
        ValidateCompoundingPeriods(compoundingPeriodsPerYear, nameof(compoundingPeriodsPerYear));
        ValidateDuration(years, nameof(years));

        if (principal == 0m)
        {
            return 0m;
        }

        if (annualRate == 0m || years == 0m)
        {
            return RoundCurrency(principal);
        }

        var ratePerPeriod = annualRate / compoundingPeriodsPerYear;
        var totalPeriods = compoundingPeriodsPerYear * years;

        ValidatePeriodCount(totalPeriods);

        var growthFactor = Pow(1m + ratePerPeriod, totalPeriods);

        return RoundCurrency(principal * growthFactor);
    }

    /// <summary>
    /// Projects a savings goal combining an initial balance with recurring end-of-month deposits
    /// (an ordinary annuity), compounded monthly.
    /// </summary>
    /// <remarks>
    /// Future value is <c>initial * (1 + r)^n + deposit * (((1 + r)^n - 1) / r)</c> where <c>r</c> is the
    /// monthly rate and <c>n</c> the number of months. When the rate is zero the annuity term degrades
    /// to <c>deposit * n</c>, which is handled explicitly to avoid division by zero.
    /// </remarks>
    /// <param name="initialBalance">The opening balance. May be zero. Must not be negative.</param>
    /// <param name="monthlyDeposit">The recurring monthly deposit. May be zero. Must not be negative.</param>
    /// <param name="annualRate">
    /// The nominal annual interest rate as a fraction (0.07m = 7%). Must be zero or greater.
    /// </param>
    /// <param name="months">The number of months to project. Must be zero or greater.</param>
    /// <returns>A <see cref="SavingsProjection"/> with the future value, contributions, and interest earned.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any monetary input is negative, the rate is negative or implausibly large, or <paramref name="months"/>
    /// is negative or exceeds the supported horizon.
    /// </exception>
    public static SavingsProjection CalculateSavingsProjection(
        decimal initialBalance,
        decimal monthlyDeposit,
        decimal annualRate,
        int months)
    {
        ValidatePrincipal(initialBalance, nameof(initialBalance));
        ValidatePrincipal(monthlyDeposit, nameof(monthlyDeposit));
        ValidateRate(annualRate, nameof(annualRate));

        if (months < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(months), months, "Months must be zero or greater.");
        }

        if (months > MaxPeriods)
        {
            throw new ArgumentOutOfRangeException(
                nameof(months), months, $"Months must not exceed {MaxPeriods}.");
        }

        var totalContributions = initialBalance + (monthlyDeposit * months);

        if (months == 0)
        {
            return new SavingsProjection(
                RoundCurrency(initialBalance),
                RoundCurrency(initialBalance),
                0m,
                0);
        }

        decimal futureValue;

        if (annualRate == 0m)
        {
            futureValue = totalContributions;
        }
        else
        {
            var monthlyRate = annualRate / 12m;
            var growthFactor = Pow(1m + monthlyRate, months);

            var futureValueOfInitial = initialBalance * growthFactor;
            var futureValueOfDeposits = monthlyDeposit == 0m
                ? 0m
                : monthlyDeposit * ((growthFactor - 1m) / monthlyRate);

            futureValue = futureValueOfInitial + futureValueOfDeposits;
        }

        var roundedFutureValue = RoundCurrency(futureValue);
        var roundedContributions = RoundCurrency(totalContributions);

        return new SavingsProjection(
            roundedFutureValue,
            roundedContributions,
            RoundCurrency(roundedFutureValue - roundedContributions),
            months);
    }

    /// <summary>
    /// Calculates the fixed monthly instalment for an amortising loan and the resulting cost breakdown:
    /// <c>payment = P * r / (1 - (1 + r)^-n)</c>.
    /// </summary>
    /// <param name="principal">The amount borrowed. Must be greater than zero.</param>
    /// <param name="annualRate">
    /// The nominal annual interest rate as a fraction (0.115m = 11.5%). Must be zero or greater;
    /// zero yields a straight-line repayment.
    /// </param>
    /// <param name="termMonths">The number of monthly instalments. Must be greater than zero.</param>
    /// <returns>A <see cref="LoanPaymentBreakdown"/> containing the monthly, total, and interest amounts.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="principal"/> is zero or negative, <paramref name="annualRate"/> is negative or
    /// implausibly large, or <paramref name="termMonths"/> is zero, negative, or exceeds the supported horizon.
    /// </exception>
    public static LoanPaymentBreakdown CalculateLoanPayment(
        decimal principal,
        decimal annualRate,
        int termMonths)
    {
        if (principal <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(principal), principal, "Loan principal must be greater than zero.");
        }

        ValidateRate(annualRate, nameof(annualRate));

        if (termMonths <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(termMonths), termMonths, "Loan term in months must be greater than zero.");
        }

        if (termMonths > MaxPeriods)
        {
            throw new ArgumentOutOfRangeException(
                nameof(termMonths), termMonths, $"Loan term in months must not exceed {MaxPeriods}.");
        }

        decimal monthlyPayment;

        if (annualRate == 0m)
        {
            monthlyPayment = principal / termMonths;
        }
        else
        {
            var monthlyRate = annualRate / 12m;
            var discountFactor = 1m - Pow(1m + monthlyRate, -termMonths);

            if (discountFactor <= 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(annualRate), annualRate,
                    "The supplied rate and term produce a degenerate amortisation factor.");
            }

            monthlyPayment = principal * monthlyRate / discountFactor;
        }

        var roundedMonthlyPayment = RoundCurrency(monthlyPayment);
        var totalPayment = RoundCurrency(roundedMonthlyPayment * termMonths);

        return new LoanPaymentBreakdown(
            roundedMonthlyPayment,
            totalPayment,
            RoundCurrency(totalPayment - principal),
            RoundCurrency(principal),
            termMonths,
            annualRate);
    }

    /// <summary>
    /// Projects a present-day monthly expense forward, compounding an annual inflation rate:
    /// <c>future = current * (1 + i)^years</c>.
    /// </summary>
    /// <param name="currentMonthlyExpense">Today's monthly expense. Must be zero or greater.</param>
    /// <param name="annualInflationRate">
    /// The annual inflation rate as a fraction (0.06m = 6%). Must be zero or greater.
    /// </param>
    /// <param name="years">How far into the future to project, in years. Must be zero or greater.</param>
    /// <returns>The inflation-adjusted monthly expense, rounded to 2 decimal places.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="currentMonthlyExpense"/> is negative, <paramref name="annualInflationRate"/> is negative
    /// or implausibly large, or <paramref name="years"/> is negative.
    /// </exception>
    public static decimal ProjectInflationAdjustedExpense(
        decimal currentMonthlyExpense,
        decimal annualInflationRate,
        decimal years)
    {
        ValidatePrincipal(currentMonthlyExpense, nameof(currentMonthlyExpense));
        ValidateRate(annualInflationRate, nameof(annualInflationRate));
        ValidateDuration(years, nameof(years));

        if (currentMonthlyExpense == 0m)
        {
            return 0m;
        }

        if (annualInflationRate == 0m || years == 0m)
        {
            return RoundCurrency(currentMonthlyExpense);
        }

        var inflationFactor = Pow(1m + annualInflationRate, years);

        return RoundCurrency(currentMonthlyExpense * inflationFactor);
    }

    /// <summary>
    /// Raises a positive <see cref="decimal"/> base to a <see cref="decimal"/> exponent without ever
    /// converting to <see cref="double"/>.
    /// </summary>
    /// <remarks>
    /// The exponent is split into its integer and fractional components. The integer component is
    /// evaluated with exact binary exponentiation (squaring); the fractional component, when present,
    /// is evaluated as <c>exp(frac * ln(base))</c> using high-precision decimal series expansions.
    /// A whole-number exponent therefore yields a mathematically exact result.
    /// </remarks>
    /// <param name="baseValue">The base. Must be greater than zero.</param>
    /// <param name="exponent">The exponent. May be negative and may contain a fractional part.</param>
    /// <returns><paramref name="baseValue"/> raised to <paramref name="exponent"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="baseValue"/> is less than or equal to zero, or the result exceeds the range of <see cref="decimal"/>.
    /// </exception>
    public static decimal Pow(decimal baseValue, decimal exponent)
    {
        if (baseValue <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseValue), baseValue, "Base must be greater than zero.");
        }

        if (exponent == 0m || baseValue == 1m)
        {
            return 1m;
        }

        var negativeExponent = exponent < 0m;
        var magnitude = negativeExponent ? -exponent : exponent;

        var wholePart = decimal.Truncate(magnitude);
        var fractionalPart = magnitude - wholePart;

        try
        {
            var result = PowInteger(baseValue, wholePart);

            if (fractionalPart != 0m)
            {
                result *= Exp(fractionalPart * Ln(baseValue));
            }

            return negativeExponent ? 1m / result : result;
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exponent), exponent,
                "The calculation overflowed the range of decimal. Reduce the rate, term, or compounding frequency.");
        }
    }

    /// <summary>
    /// Exact exponentiation by squaring for a non-negative whole exponent.
    /// </summary>
    /// <param name="baseValue">The base.</param>
    /// <param name="wholeExponent">A non-negative whole number exponent.</param>
    /// <returns>The exact power.</returns>
    private static decimal PowInteger(decimal baseValue, decimal wholeExponent)
    {
        var result = 1m;
        var factor = baseValue;
        var remaining = wholeExponent;

        while (remaining > 0m)
        {
            if (decimal.Remainder(remaining, 2m) != 0m)
            {
                result *= factor;
                remaining -= 1m;

                if (remaining == 0m)
                {
                    break;
                }
            }

            remaining /= 2m;
            factor *= factor;
        }

        return result;
    }

    /// <summary>
    /// Computes the natural logarithm of a positive <see cref="decimal"/> using the fast-converging
    /// area-hyperbolic-tangent series after range-reducing the argument toward 1.
    /// </summary>
    /// <param name="value">The value. Must be greater than zero.</param>
    /// <returns>The natural logarithm of <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero.</exception>
    public static decimal Ln(decimal value)
    {
        if (value <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, "Logarithm is undefined for values less than or equal to zero.");
        }

        if (value == 1m)
        {
            return 0m;
        }

        // ln(2) to 28 significant digits.
        const decimal Ln2 = 0.6931471805599453094172321215m;

        var exponentOfTwo = 0;
        var reduced = value;

        while (reduced > 1.5m)
        {
            reduced /= 2m;
            exponentOfTwo++;
        }

        while (reduced < 0.5m)
        {
            reduced *= 2m;
            exponentOfTwo--;
        }

        // atanh series: ln(x) = 2 * sum_{k odd} z^k / k, where z = (x - 1) / (x + 1).
        var z = (reduced - 1m) / (reduced + 1m);
        var zSquared = z * z;
        var term = z;
        var sum = z;

        for (var k = 3; k <= 601; k += 2)
        {
            term *= zSquared;
            var addition = term / k;

            if (addition == 0m)
            {
                break;
            }

            sum += addition;
        }

        return (2m * sum) + (exponentOfTwo * Ln2);
    }

    /// <summary>
    /// Computes e raised to a <see cref="decimal"/> power using a range-reduced Taylor expansion.
    /// </summary>
    /// <param name="value">The exponent.</param>
    /// <returns>e raised to <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The result exceeds the range of <see cref="decimal"/>.</exception>
    public static decimal Exp(decimal value)
    {
        if (value == 0m)
        {
            return 1m;
        }

        // e to 28 significant digits.
        const decimal E = 2.7182818284590452353602874714m;

        var wholePart = decimal.Truncate(value);
        var fractionalPart = value - wholePart;

        decimal result;

        try
        {
            result = wholePart switch
            {
                0m => 1m,
                > 0m => PowInteger(E, wholePart),
                _ => 1m / PowInteger(E, -wholePart)
            };
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, "The exponential result exceeds the range of decimal.");
        }

        if (fractionalPart != 0m)
        {
            // Taylor series for the fractional remainder, where |fractionalPart| < 1 guarantees convergence.
            var term = 1m;
            var sum = 1m;

            for (var k = 1; k <= 40; k++)
            {
                term = term * fractionalPart / k;

                if (term == 0m)
                {
                    break;
                }

                sum += term;
            }

            result *= sum;
        }

        return result;
    }
}
