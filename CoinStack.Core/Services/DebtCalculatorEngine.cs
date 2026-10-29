namespace CoinStack.Services;

/// <summary>
/// Works out the missing values of a debt (payment, term, principal, total repaid) from the ones given.
/// </summary>
/// <remarks>
/// Each interest type has its own maths:
/// <list type="bullet">
/// <item>Simple: flat interest of principal × rate × years, repaid in equal instalments.</item>
/// <item>Amortizing and Compound: interest on the outstanding balance each month, at the monthly rate
/// equivalent to the compounding frequency (monthly, daily for APR, or once a year).</item>
/// <item>None: the principal split into equal instalments.</item>
/// </list>
/// The annual rate is always a percentage, so 1 means 1% a year, never 100%.
/// </remarks>
public sealed class DebtCalculatorEngine : IDebtCalculatorEngine
{
    public const decimal MaxAmount = 1_000_000_000_000m;
    public const decimal MaxAnnualRatePercent = 1000m;
    public const int MaxTermMonths = 1200;

    private const decimal Cent = 0.005m;

    private static readonly decimal[] VariableRateShocks = [-1m, 1m, 2m];

    public DebtCalculationResult Calculate(DebtCalculationInput input)
    {
        try
        {
            return CalculateCore(input);
        }
        catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException)
        {
            return Invalid(input, ["These numbers are too large to calculate. Check the amounts, rate, term and dates."]);
        }
    }

    private static DebtCalculationResult CalculateCore(DebtCalculationInput input)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var inferences = new List<string>();

        ValidateBasicInput(input, errors);
        if (errors.Count > 0)
        {
            return Invalid(input, errors);
        }

        var annualRate = input.AnnualRatePercent / 100m;
        var startDate = input.StartDate?.Date;
        var endDate = input.EndDate?.Date;

        var terms = new LoanTerms
        {
            Principal = input.Principal,
            Payment = input.MonthlyPayment,
            TotalOwed = input.TotalOwed,
            TermMonths = input.TermMonths
        };

        if (!terms.TermMonths.HasValue && startDate.HasValue && endDate.HasValue)
        {
            terms.TermMonths = CalculateMonthSpan(startDate.Value, endDate.Value);
            inferences.Add("Derived term from start and end dates.");
        }

        if (!startDate.HasValue && endDate.HasValue && terms.TermMonths.HasValue)
        {
            startDate = endDate.Value.AddMonths(-terms.TermMonths.Value);
            inferences.Add("Derived start date from end date and term.");
        }

        if (terms.TermMonths > MaxTermMonths)
        {
            return Invalid(input, [$"The term can be at most {MaxTermMonths} months (100 years)."]);
        }

        var resolvedType = ResolveInterestType(input.InterestType, annualRate);
        DebtCompoundingFrequency? frequency = resolvedType is DebtInterestType.Compound or DebtInterestType.Amortizing
            ? input.CompoundingFrequency ?? DebtCompoundingFrequency.Monthly
            : null;

        if (resolvedType != DebtInterestType.None && !annualRate.HasValue)
        {
            return Invalid(input, ["Enter the interest rate for this debt type."]);
        }

        decimal monthlyRate = resolvedType switch
        {
            DebtInterestType.None => 0m,
            DebtInterestType.Simple => annualRate!.Value / 12m,
            _ => EffectiveMonthlyRate(annualRate!.Value, frequency!.Value)
        };

        switch (resolvedType)
        {
            case DebtInterestType.None:
                SolveNoInterest(terms, inferences, warnings, errors);
                break;

            case DebtInterestType.Simple:
                SolveSimple(terms, monthlyRate, inferences, warnings, errors);
                break;

            default:
                SolveOnBalance(terms, monthlyRate, inferences, warnings, errors);
                break;
        }

        if (errors.Count > 0)
        {
            return Invalid(input, errors, inferences);
        }

        var principal = terms.Principal!.Value;
        var payment = terms.Payment!.Value;
        var termMonths = terms.TermMonths!.Value;
        var totalOwed = terms.TotalRepaid;

        if (!endDate.HasValue && startDate.HasValue)
        {
            endDate = startDate.Value.AddMonths(termMonths);
            inferences.Add("Derived end date from start date and term.");
        }

        decimal? remainingBalance = null;
        if (input.PaymentsMade is { } paymentsMade)
        {
            remainingBalance = resolvedType is DebtInterestType.Simple or DebtInterestType.None
                ? Math.Max(0m, totalOwed - (paymentsMade * payment))
                : Simulate(principal, monthlyRate, payment, paymentsMade).Balance;
        }

        var rateScenarios = new List<DebtRateScenario>();
        if (input.IsVariableRate && frequency.HasValue && annualRate.HasValue)
        {
            foreach (var shock in VariableRateShocks)
            {
                var shockedRate = annualRate.Value + (shock / 100m);
                if (shockedRate < 0m)
                {
                    continue;
                }

                var shockedMonthly = EffectiveMonthlyRate(shockedRate, frequency.Value);
                var shockedPayment = Instalment(principal, shockedMonthly, termMonths);
                var shockedTotal = Simulate(principal, shockedMonthly, shockedPayment, termMonths).Paid;
                rateScenarios.Add(new DebtRateScenario(
                    shock,
                    shockedRate,
                    RoundCurrency(shockedPayment),
                    RoundCurrency(shockedTotal),
                    RoundCurrency(shockedTotal - principal)));
            }
        }

        AmortisationScheduleResult? schedule = null;
        if (input.FirstPaymentDate.HasValue)
        {
            var flatInterest = principal * monthlyRate;
            schedule = BuildAmortisationSchedule(
                principal,
                payment,
                input.FirstPaymentDate.Value.Date,
                resolvedType == DebtInterestType.Simple ? _ => flatInterest : balance => balance * monthlyRate);
        }

        return new DebtCalculationResult
        {
            IsValid = true,
            Errors = errors,
            Warnings = warnings,
            Inferences = inferences,
            ResolvedInterestType = resolvedType,
            CompoundingFrequency = frequency,
            Principal = RoundCurrency(principal),
            InterestRate = annualRate,
            EffectiveMonthlyRate = decimal.Round(monthlyRate, 8),
            MonthlyPayment = RoundCurrency(payment),
            TotalOwed = RoundCurrency(totalOwed),
            InterestAmount = RoundCurrency(totalOwed - principal),
            RemainingBalanceAfterPayments = remainingBalance.HasValue ? RoundCurrency(remainingBalance.Value) : null,
            TermMonths = termMonths,
            Years = decimal.Round(termMonths / 12m, 2),
            StartDate = startDate,
            EndDate = endDate,
            FormulaUsed = GetFormulaLabel(resolvedType),
            AmortisationSchedule = schedule,
            RateScenarios = rateScenarios
        };
    }

    /// <summary>Interest on the outstanding balance every month (amortizing and compound debt).</summary>
    private static void SolveOnBalance(
        LoanTerms terms,
        decimal monthlyRate,
        List<string> inferences,
        List<string> warnings,
        List<string> errors)
    {
        if (!terms.Principal.HasValue && terms.TotalOwed.HasValue)
        {
            terms.Principal = terms.TotalOwed;
            inferences.Add("Used total owed as the outstanding balance.");
        }

        if (!terms.Principal.HasValue && terms.Payment.HasValue && terms.TermMonths.HasValue)
        {
            terms.Principal = PresentValue(terms.Payment.Value, monthlyRate, terms.TermMonths.Value);
            inferences.Add("Derived principal from payment, rate and term.");
        }

        if (!terms.Principal.HasValue)
        {
            errors.Add("Enter the principal (or total owed), or a monthly payment and term.");
            return;
        }

        var principal = terms.Principal.Value;

        if (terms.Payment.HasValue && monthlyRate > 0m && terms.Payment.Value <= principal * monthlyRate)
        {
            errors.Add("The monthly payment is too low to cover the monthly interest, so the debt would never be repaid.");
            return;
        }

        if (!terms.TermMonths.HasValue && terms.Payment.HasValue)
        {
            var months = MonthsToRepay(principal, monthlyRate, terms.Payment.Value);
            if (months > MaxTermMonths)
            {
                errors.Add("At this payment the debt would take more than 100 years to repay.");
                return;
            }

            terms.TermMonths = months;
            inferences.Add("Derived term from principal, payment and rate.");
        }

        if (!terms.Payment.HasValue && terms.TermMonths.HasValue)
        {
            terms.Payment = Instalment(principal, monthlyRate, terms.TermMonths.Value);
            inferences.Add("Derived monthly payment from principal, rate and term.");
        }

        if (!terms.Payment.HasValue || !terms.TermMonths.HasValue)
        {
            errors.Add("Enter a term or a monthly payment.");
            return;
        }

        var outcome = Simulate(principal, monthlyRate, terms.Payment.Value, terms.TermMonths.Value);
        if (outcome.Balance > Cent)
        {
            warnings.Add($"This payment doesn't clear the debt within the term: {outcome.Balance:N2} would still be owed after {terms.TermMonths} months.");
            terms.TotalRepaid = outcome.Paid + outcome.Balance;
            return;
        }

        if (outcome.Months < terms.TermMonths)
        {
            warnings.Add($"At this payment the debt is repaid after {outcome.Months} months, before the {terms.TermMonths}-month term.");
            terms.TermMonths = outcome.Months;
        }

        terms.TotalRepaid = outcome.Paid;
    }

    /// <summary>Flat interest: principal × rate × years, fixed up front and spread over the instalments.</summary>
    private static void SolveSimple(
        LoanTerms terms,
        decimal monthlyRate,
        List<string> inferences,
        List<string> warnings,
        List<string> errors)
    {
        if (!terms.Principal.HasValue && terms.TotalOwed.HasValue)
        {
            terms.Principal = terms.TotalOwed;
            inferences.Add("Used total owed as the amount borrowed.");
        }

        if (!terms.Principal.HasValue && terms.Payment.HasValue && terms.TermMonths.HasValue)
        {
            // payment × n = P × (1 + monthlyRate × n)
            var n = terms.TermMonths.Value;
            terms.Principal = terms.Payment.Value * n / (1m + (monthlyRate * n));
            inferences.Add("Derived principal from payment, rate and term.");
        }

        if (!terms.Principal.HasValue)
        {
            errors.Add("Enter the principal (or total owed), or a monthly payment and term.");
            return;
        }

        var principal = terms.Principal.Value;
        var interestPerMonth = principal * monthlyRate;

        if (!terms.TermMonths.HasValue && terms.Payment.HasValue)
        {
            if (terms.Payment.Value <= interestPerMonth)
            {
                errors.Add("The monthly payment is too low to cover the monthly interest, so the debt would never be repaid.");
                return;
            }

            var months = CeilingMonths(principal / (terms.Payment.Value - interestPerMonth));
            if (months > MaxTermMonths)
            {
                errors.Add("At this payment the debt would take more than 100 years to repay.");
                return;
            }

            terms.TermMonths = months;
            inferences.Add("Derived term from principal, payment and rate.");
        }

        if (!terms.TermMonths.HasValue)
        {
            errors.Add("Enter a term or a monthly payment.");
            return;
        }

        var term = terms.TermMonths.Value;
        var total = principal + (interestPerMonth * term);

        if (!terms.Payment.HasValue)
        {
            terms.Payment = total / term;
            inferences.Add("Derived monthly payment as (principal + interest) ÷ term.");
        }

        var scheduled = terms.Payment.Value * term;
        if (scheduled < total - Cent)
        {
            warnings.Add($"This payment doesn't clear the debt within the term: {total - scheduled:N2} would still be owed after {term} months.");
        }
        else if (scheduled - total >= terms.Payment.Value)
        {
            warnings.Add($"At this payment the total is repaid after {CeilingMonths(total / terms.Payment.Value)} months, before the {term}-month term.");
        }

        terms.TotalRepaid = total;
    }

    private static void SolveNoInterest(
        LoanTerms terms,
        List<string> inferences,
        List<string> warnings,
        List<string> errors)
    {
        if (!terms.Principal.HasValue && terms.TotalOwed.HasValue)
        {
            terms.Principal = terms.TotalOwed;
            inferences.Add("Used total owed as the principal (no-interest debt).");
        }

        if (!terms.Principal.HasValue && terms.Payment.HasValue && terms.TermMonths.HasValue)
        {
            terms.Principal = terms.Payment.Value * terms.TermMonths.Value;
            inferences.Add("Derived total owed as monthly payment × term.");
        }

        if (!terms.Principal.HasValue)
        {
            errors.Add("Enter the principal or total owed for a no-interest debt.");
            return;
        }

        var principal = terms.Principal.Value;

        if (!terms.TermMonths.HasValue && terms.Payment.HasValue)
        {
            var months = CeilingMonths(principal / terms.Payment.Value);
            if (months > MaxTermMonths)
            {
                errors.Add("At this payment the debt would take more than 100 years to repay.");
                return;
            }

            terms.TermMonths = months;
            inferences.Add("Derived term from total owed and monthly payment.");
        }

        if (!terms.TermMonths.HasValue)
        {
            errors.Add("Enter a term or a monthly payment.");
            return;
        }

        if (!terms.Payment.HasValue)
        {
            terms.Payment = principal / terms.TermMonths.Value;
            inferences.Add("Derived monthly payment from total owed and term.");
        }

        var scheduled = terms.Payment.Value * terms.TermMonths.Value;
        if (scheduled < principal - Cent)
        {
            warnings.Add($"This payment doesn't clear the debt within the term: {principal - scheduled:N2} would still be owed after {terms.TermMonths} months.");
        }
        else if (scheduled - principal >= terms.Payment.Value)
        {
            var months = CeilingMonths(principal / terms.Payment.Value);
            warnings.Add($"At this payment the debt is repaid after {months} months, before the {terms.TermMonths}-month term.");
            terms.TermMonths = months;
        }

        terms.TotalRepaid = principal;
    }

    /// <summary>The monthly rate that matches <paramref name="annualRate"/> at the given compounding.</summary>
    internal static decimal EffectiveMonthlyRate(decimal annualRate, DebtCompoundingFrequency frequency)
    {
        return frequency switch
        {
            // Interest added once a year: the monthly rate that grows to the annual rate over 12 months.
            DebtCompoundingFrequency.Annual => (decimal)(Math.Pow(1d + (double)annualRate, 1d / 12d) - 1d),
            // Daily interest (APR): 365/12 days of daily compounding in an average month.
            DebtCompoundingFrequency.Daily => (decimal)(Math.Pow(1d + ((double)annualRate / 365d), 365d / 12d) - 1d),
            _ => annualRate / 12m
        };
    }

    /// <summary>The level payment that clears <paramref name="principal"/> in <paramref name="months"/>.</summary>
    private static decimal Instalment(decimal principal, decimal monthlyRate, int months)
    {
        if (monthlyRate == 0m)
        {
            return principal / months;
        }

        var discount = (decimal)Math.Pow(1d + (double)monthlyRate, -months);
        return principal * monthlyRate / (1m - discount);
    }

    private static decimal PresentValue(decimal payment, decimal monthlyRate, int months)
    {
        if (monthlyRate == 0m)
        {
            return payment * months;
        }

        var discount = (decimal)Math.Pow(1d + (double)monthlyRate, -months);
        return payment * (1m - discount) / monthlyRate;
    }

    /// <summary>Months until the balance is cleared; the caller has checked the payment beats the interest.</summary>
    private static int MonthsToRepay(decimal principal, decimal monthlyRate, decimal payment)
    {
        if (monthlyRate == 0m)
        {
            return CeilingMonths(principal / payment);
        }

        var n = -Math.Log(1d - (double)(principal * monthlyRate / payment)) / Math.Log(1d + (double)monthlyRate);
        return double.IsFinite(n) && n < MaxTermMonths ? CeilingMonths((decimal)n) : MaxTermMonths + 1;
    }

    /// <summary>Rounds a month count up, ignoring floating-point noise such as 36.0000001.</summary>
    private static int CeilingMonths(decimal months)
    {
        if (months > MaxTermMonths)
        {
            return MaxTermMonths + 1;
        }

        return Math.Max(1, (int)Math.Ceiling(months - 0.000001m));
    }

    /// <summary>Runs the balance forward month by month: interest is added, then the payment taken off.</summary>
    private static (int Months, decimal Paid, decimal Balance) Simulate(decimal principal, decimal monthlyRate, decimal payment, int maxMonths)
    {
        var balance = principal;
        var paid = 0m;
        var months = 0;

        while (balance > Cent && months < maxMonths)
        {
            months++;
            var due = balance + (balance * monthlyRate);
            var paidNow = Math.Min(payment, due);
            paid += paidNow;
            balance = due - paidNow;
        }

        return (months, paid, Math.Max(0m, balance));
    }

    private static AmortisationScheduleResult BuildAmortisationSchedule(
        decimal principal,
        decimal monthlyPayment,
        DateTime firstPaymentDate,
        Func<decimal, decimal> interestFor)
    {
        var today = DateTime.Today;
        var rows = new List<AmortisationRow>();
        var balance = principal;

        for (var n = 1; balance > Cent && rows.Count < MaxTermMonths; n++)
        {
            var paymentDate = firstPaymentDate.AddMonths(n - 1);
            var startingBalance = balance;
            var interest = decimal.Round(interestFor(balance), 2);
            var payment = decimal.Round(Math.Min(monthlyPayment, balance + interest), 2);
            var principalPaid = decimal.Round(Math.Max(0m, payment - interest), 2);
            var endingBalance = decimal.Round(Math.Max(0m, startingBalance - principalPaid), 2);

            rows.Add(new AmortisationRow
            {
                PaymentNumber = n,
                PaymentDate = paymentDate,
                StartingBalance = startingBalance,
                Payment = payment,
                InterestPortion = interest,
                PrincipalPortion = principalPaid,
                EndingBalance = endingBalance,
                IsPaid = paymentDate.Date < today
            });

            if (endingBalance >= startingBalance)
            {
                // The payment no longer reduces the balance; stop rather than list the same row forever.
                break;
            }

            balance = endingBalance;
        }

        var paidRows = rows.Where(r => r.IsPaid).ToList();
        var paymentsMade = paidRows.Count;
        var totalPrincipalRepaid = decimal.Round(paidRows.Sum(r => r.PrincipalPortion), 2);

        return new AmortisationScheduleResult
        {
            Rows = rows,
            TotalInterestPaidToDate = decimal.Round(paidRows.Sum(r => r.InterestPortion), 2),
            TotalPrincipalRepaidToDate = totalPrincipalRepaid,
            TotalAmountPaidToDate = decimal.Round(paidRows.Sum(r => r.Payment), 2),
            CurrentRemainingBalance = paymentsMade > 0 ? paidRows[^1].EndingBalance : principal,
            PaymentsMadeCount = paymentsMade,
            PaymentsRemainingCount = rows.Count - paymentsMade,
            PayoffDate = rows.LastOrDefault()?.PaymentDate,
            PercentageCompleted = principal > 0 ? decimal.Round(totalPrincipalRepaid / principal * 100m, 2) : 0m
        };
    }

    private static DebtInterestType ResolveInterestType(DebtInterestType? explicitType, decimal? annualRate)
    {
        if (explicitType.HasValue)
        {
            return explicitType.Value;
        }

        return annualRate is > 0m ? DebtInterestType.Amortizing : DebtInterestType.None;
    }

    private static int CalculateMonthSpan(DateTime start, DateTime end)
    {
        var months = ((end.Year - start.Year) * 12) + end.Month - start.Month;
        if (end.Day > start.Day)
        {
            months++;
        }

        return Math.Max(1, months);
    }

    private static string GetFormulaLabel(DebtInterestType type)
    {
        return type switch
        {
            DebtInterestType.Simple => "interest = P × rate × years, payment = (P + interest) ÷ n",
            DebtInterestType.Compound or DebtInterestType.Amortizing => "payment = P × i ÷ (1 − (1 + i)^−n), i = monthly rate",
            DebtInterestType.None => "payment = total owed ÷ n",
            _ => ""
        };
    }

    private static void ValidateBasicInput(DebtCalculationInput input, List<string> errors)
    {
        if (input.Principal < 0)
        {
            errors.Add("Principal cannot be negative.");
        }

        if (input.MonthlyPayment <= 0)
        {
            errors.Add("Monthly payment must be greater than zero.");
        }

        if (input.TotalOwed <= 0)
        {
            errors.Add("Total owed must be greater than zero.");
        }

        if (input.Principal > MaxAmount || input.MonthlyPayment > MaxAmount || input.TotalOwed > MaxAmount)
        {
            errors.Add("Amounts can be at most 1 000 000 000 000.");
        }

        if (input.AnnualRatePercent < 0)
        {
            errors.Add("Interest rate cannot be negative.");
        }

        if (input.AnnualRatePercent > MaxAnnualRatePercent)
        {
            errors.Add($"Interest rate can be at most {MaxAnnualRatePercent:0}% a year.");
        }

        if (input.TermMonths <= 0)
        {
            errors.Add("Term months must be greater than zero.");
        }

        if (input.TermMonths > MaxTermMonths)
        {
            errors.Add($"The term can be at most {MaxTermMonths} months (100 years).");
        }

        if (input.StartDate.HasValue && input.EndDate.HasValue && input.EndDate <= input.StartDate)
        {
            errors.Add("End date must be after start date.");
        }

        if (input.PaymentsMade < 0)
        {
            errors.Add("Payments made cannot be negative.");
        }

        if (input.PaymentsMade > MaxTermMonths)
        {
            errors.Add($"Payments made can be at most {MaxTermMonths}.");
        }
    }

    private static DebtCalculationResult Invalid(DebtCalculationInput input, List<string> errors, List<string>? inferences = null)
    {
        return new DebtCalculationResult
        {
            IsValid = false,
            Errors = errors,
            Inferences = inferences ?? [],
            ResolvedInterestType = input.InterestType ?? DebtInterestType.Amortizing
        };
    }

    private static decimal RoundCurrency(decimal value) => decimal.Round(value, 2);

    private sealed class LoanTerms
    {
        public decimal? Principal { get; set; }
        public decimal? Payment { get; set; }
        public decimal? TotalOwed { get; set; }
        public int? TermMonths { get; set; }

        /// <summary>Principal plus all interest, filled in once the terms are solved.</summary>
        public decimal TotalRepaid { get; set; }
    }
}
