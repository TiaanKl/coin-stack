namespace CoinStack.Services;

public sealed class DebtCalculatorEngine : IDebtCalculatorEngine
{
    public DebtCalculationResult Calculate(DebtCalculationInput input)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var inferences = new List<string>();

        var principal = input.Principal;
        var interestRate = NormalizeInterestRate(input.InterestRate);
        var monthlyPayment = input.MonthlyPayment;
        var totalOwed = input.TotalOwed;
        var startDate = input.StartDate?.Date;
        var endDate = input.EndDate?.Date;
        var termMonths = input.TermMonths;
        var paymentsMade = input.PaymentsMade;

        ValidateBasicInput(input, errors);

        if (!termMonths.HasValue && startDate.HasValue && endDate.HasValue)
        {
            termMonths = CalculateMonthSpan(startDate.Value, endDate.Value);
            if (termMonths.HasValue)
            {
                inferences.Add("Derived term from start and end dates.");
            }
        }

        if (!endDate.HasValue && startDate.HasValue && termMonths.HasValue)
        {
            endDate = startDate.Value.AddMonths(termMonths.Value);
            inferences.Add("Derived end date from start date and term.");
        }

        if (!startDate.HasValue && endDate.HasValue && termMonths.HasValue)
        {
            startDate = endDate.Value.AddMonths(-termMonths.Value);
            inferences.Add("Derived start date from end date and term.");
        }

        var resolvedType = ResolveInterestType(input.InterestType, interestRate);
        DebtCompoundingFrequency? compoundingFrequency = resolvedType == DebtInterestType.Compound
            ? input.CompoundingFrequency ?? DebtCompoundingFrequency.Monthly
            : null;

        decimal? years = termMonths.HasValue ? termMonths.Value / 12m : null;

        switch (resolvedType)
        {
            case DebtInterestType.None:
                CalculateNoInterest(ref principal, ref monthlyPayment, ref totalOwed, ref termMonths, inferences, errors);
                break;

            case DebtInterestType.Simple:
                CalculateSimpleInterest(ref principal, interestRate, ref monthlyPayment, ref totalOwed, ref termMonths, ref years, inferences, errors);
                break;

            case DebtInterestType.Compound:
                CalculateCompoundInterest(ref principal, interestRate, compoundingFrequency, ref monthlyPayment, ref totalOwed, ref termMonths, ref years, inferences, errors);
                break;

            case DebtInterestType.Amortizing:
                CalculateAmortizing(ref principal, interestRate, ref monthlyPayment, ref totalOwed, ref termMonths, inferences, errors);
                break;
        }

        if (principal.HasValue && totalOwed.HasValue && totalOwed < principal)
        {
            warnings.Add("Total owed is less than principal. Check inputs or debt type selection.");
        }

        if (termMonths.HasValue && termMonths <= 0)
        {
            errors.Add("Term must be greater than zero months.");
        }

        years = termMonths.HasValue ? termMonths.Value / 12m : years;
        decimal? interestAmount = principal.HasValue && totalOwed.HasValue
            ? decimal.Round(totalOwed.Value - principal.Value, 2)
            : null;

        decimal? remainingBalance = null;
        if (paymentsMade.HasValue && paymentsMade.Value >= 0 && monthlyPayment.HasValue)
        {
            remainingBalance = CalculateRemainingBalance(resolvedType, principal, totalOwed, interestRate, monthlyPayment.Value, paymentsMade.Value, termMonths, compoundingFrequency);
            if (remainingBalance.HasValue)
            {
                remainingBalance = decimal.Round(Math.Max(0m, remainingBalance.Value), 2);
            }
        }

        AmortisationScheduleResult? amortisationSchedule = null;
        if (errors.Count == 0
            && input.FirstPaymentDate.HasValue
            && principal.HasValue
            && monthlyPayment.HasValue
            && resolvedType is DebtInterestType.Simple or DebtInterestType.Amortizing or DebtInterestType.None)
        {
            amortisationSchedule = BuildAmortisationSchedule(
                principal.Value,
                interestRate,
                monthlyPayment.Value,
                input.FirstPaymentDate.Value.Date);
        }

        return new DebtCalculationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            Inferences = inferences,
            ResolvedInterestType = resolvedType,
            CompoundingFrequency = compoundingFrequency,
            Principal = RoundCurrency(principal),
            InterestRate = interestRate,
            MonthlyPayment = RoundCurrency(monthlyPayment),
            TotalOwed = RoundCurrency(totalOwed),
            InterestAmount = RoundCurrency(interestAmount),
            RemainingBalanceAfterPayments = RoundCurrency(remainingBalance),
            TermMonths = termMonths,
            Years = years.HasValue ? decimal.Round(years.Value, 2) : null,
            StartDate = startDate,
            EndDate = endDate,
            FormulaUsed = GetFormulaLabel(resolvedType),
            AmortisationSchedule = amortisationSchedule
        };
    }

    private static void CalculateNoInterest(
        ref decimal? principal,
        ref decimal? monthlyPayment,
        ref decimal? totalOwed,
        ref int? termMonths,
        List<string> inferences,
        List<string> errors)
    {
        if (!principal.HasValue && totalOwed.HasValue)
        {
            principal = totalOwed;
            inferences.Add("Derived principal from total owed (no-interest debt).");
        }

        if (!totalOwed.HasValue && principal.HasValue)
        {
            totalOwed = principal;
            inferences.Add("Derived total owed from principal (no-interest debt).");
        }

        if (!totalOwed.HasValue && monthlyPayment.HasValue && termMonths.HasValue)
        {
            totalOwed = monthlyPayment.Value * termMonths.Value;
            inferences.Add("Derived total owed using monthlyPayment × termMonths.");
        }

        if (!monthlyPayment.HasValue && totalOwed.HasValue && termMonths.HasValue && termMonths > 0)
        {
            monthlyPayment = totalOwed.Value / termMonths.Value;
            inferences.Add("Derived monthly payment from total owed and term.");
        }

        if (!termMonths.HasValue && monthlyPayment.HasValue && totalOwed.HasValue && monthlyPayment > 0)
        {
            termMonths = (int)Math.Ceiling(totalOwed.Value / monthlyPayment.Value);
            inferences.Add("Derived term from total owed and monthly payment.");
        }

        if (!principal.HasValue && !totalOwed.HasValue)
        {
            errors.Add("Provide principal or total owed for a no-interest debt.");
        }
    }

    private static void CalculateSimpleInterest(
        ref decimal? principal,
        decimal? interestRate,
        ref decimal? monthlyPayment,
        ref decimal? totalOwed,
        ref int? termMonths,
        ref decimal? years,
        List<string> inferences,
        List<string> errors)
    {
        if (!interestRate.HasValue)
        {
            errors.Add("Interest rate is required for simple-interest debt.");
            return;
        }

        var r = interestRate.Value / 12m;

        if (!principal.HasValue && totalOwed.HasValue)
        {
            principal = totalOwed;
            totalOwed = null;
            inferences.Add("Treated total owed as principal (starting balance).");
        }

        if (!termMonths.HasValue && years.HasValue && years > 0)
        {
            termMonths = (int)Math.Ceiling(years.Value * 12m);
        }

        if (!termMonths.HasValue && principal.HasValue && monthlyPayment.HasValue)
        {
            if (r == 0)
            {
                termMonths = (int)Math.Ceiling(principal.Value / monthlyPayment.Value);
            }
            else
            {
                if (monthlyPayment.Value <= principal.Value * r)
                {
                    errors.Add("Monthly payment is too low to offset monthly interest.");
                    return;
                }

                var n = -Math.Log(1d - (double)(principal.Value * r / monthlyPayment.Value)) / Math.Log((double)(1m + r));
                termMonths = (int)Math.Ceiling((decimal)n);
            }

            years = termMonths.Value / 12m;
            inferences.Add("Derived term from principal, interest rate, and monthly payment.");
        }

        if (!termMonths.HasValue || termMonths <= 0)
        {
            errors.Add("Provide term (months) or start/end dates for simple-interest calculations.");
            return;
        }

        years = termMonths.Value / 12m;

        if (!principal.HasValue && monthlyPayment.HasValue)
        {
            if (r == 0)
            {
                principal = monthlyPayment.Value * termMonths.Value;
            }
            else
            {
                var pow = (decimal)Math.Pow((double)(1m + r), -termMonths.Value);
                principal = monthlyPayment.Value * (1m - pow) / r;
            }

            inferences.Add("Derived principal from payment, rate, and term.");
        }

        if (!principal.HasValue)
        {
            errors.Add("Provide principal or total owed for simple-interest calculations.");
            return;
        }

        if (!monthlyPayment.HasValue)
        {
            if (r == 0)
            {
                monthlyPayment = principal.Value / termMonths.Value;
            }
            else
            {
                var denominator = 1m - (decimal)Math.Pow((double)(1m + r), -termMonths.Value);
                if (denominator == 0)
                {
                    errors.Add("Unable to derive monthly payment with the provided values.");
                    return;
                }

                monthlyPayment = (principal.Value * r) / denominator;
            }

            inferences.Add("Derived monthly payment from principal, rate, and term.");
        }

        totalOwed = monthlyPayment.Value * termMonths.Value;
        inferences.Add("Derived total owed as sum of all monthly payments.");
    }

    private static void CalculateCompoundInterest(
        ref decimal? principal,
        decimal? interestRate,
        DebtCompoundingFrequency? compoundingFrequency,
        ref decimal? monthlyPayment,
        ref decimal? totalOwed,
        ref int? termMonths,
        ref decimal? years,
        List<string> inferences,
        List<string> errors)
    {
        if (!interestRate.HasValue)
        {
            errors.Add("Interest rate is required for compound-interest debt.");
            return;
        }

        var n = compoundingFrequency == DebtCompoundingFrequency.Annual ? 1m : 12m;

        if (!years.HasValue && termMonths.HasValue)
        {
            years = termMonths.Value / 12m;
        }

        if (!years.HasValue && principal.HasValue && totalOwed.HasValue && principal > 0)
        {
            var growthRatio = totalOwed.Value / principal.Value;
            if (growthRatio <= 0)
            {
                errors.Add("Cannot derive term from non-positive principal/total values.");
                return;
            }

            var numerator = Math.Log((double)growthRatio);
            var denominator = (double)(n * (decimal)Math.Log((double)(1m + (interestRate.Value / n))));
            if (denominator <= 0)
            {
                errors.Add("Unable to derive term with the selected compound settings.");
                return;
            }

            years = (decimal)(numerator / denominator);
            termMonths = (int)Math.Ceiling(years.Value * 12m);
            inferences.Add("Derived term from principal, total owed, and compound-interest settings.");
        }

        if (!years.HasValue && monthlyPayment.HasValue && totalOwed.HasValue && monthlyPayment > 0)
        {
            termMonths = (int)Math.Ceiling(totalOwed.Value / monthlyPayment.Value);
            years = termMonths.Value / 12m;
            inferences.Add("Derived term from total owed and monthly payment.");
        }

        if (!years.HasValue || years <= 0)
        {
            errors.Add("Provide term (months) or enough values to derive it for compound interest.");
            return;
        }

        var factor = (decimal)Math.Pow((double)(1m + (interestRate.Value / n)), (double)(n * years.Value));

        if (!principal.HasValue && totalOwed.HasValue)
        {
            principal = totalOwed.Value / factor;
            inferences.Add("Derived principal from total owed and compound-interest factor.");
        }

        if (!totalOwed.HasValue && principal.HasValue)
        {
            totalOwed = principal.Value * factor;
            inferences.Add("Derived total owed using compound-interest formula.");
        }

        if (!monthlyPayment.HasValue && totalOwed.HasValue && termMonths.HasValue && termMonths > 0)
        {
            monthlyPayment = totalOwed.Value / termMonths.Value;
            inferences.Add("Derived monthly payment from total owed and term.");
        }

        if (!termMonths.HasValue && monthlyPayment.HasValue && totalOwed.HasValue && monthlyPayment > 0)
        {
            termMonths = (int)Math.Ceiling(totalOwed.Value / monthlyPayment.Value);
            inferences.Add("Derived term from total owed and monthly payment.");
        }
    }

    private static void CalculateAmortizing(
        ref decimal? principal,
        decimal? interestRate,
        ref decimal? monthlyPayment,
        ref decimal? totalOwed,
        ref int? termMonths,
        List<string> inferences,
        List<string> errors)
    {
        if (!interestRate.HasValue)
        {
            errors.Add("Interest rate is required for amortizing-loan calculations.");
            return;
        }

        if (!principal.HasValue && totalOwed.HasValue)
        {
            principal = totalOwed;
            inferences.Add("Derived principal from total owed for amortizing loan.");
        }

        if (!principal.HasValue && monthlyPayment.HasValue && termMonths.HasValue)
        {
            var r = interestRate.Value / 12m;
            if (r == 0)
            {
                principal = monthlyPayment.Value * termMonths.Value;
            }
            else
            {
                var pow = (decimal)Math.Pow((double)(1m + r), -termMonths.Value);
                principal = monthlyPayment.Value * (1m - pow) / r;
            }

            inferences.Add("Derived principal from payment, rate, and term.");
        }

        if (!termMonths.HasValue && principal.HasValue && monthlyPayment.HasValue)
        {
            var r = interestRate.Value / 12m;
            if (r == 0)
            {
                termMonths = (int)Math.Ceiling(principal.Value / monthlyPayment.Value);
            }
            else
            {
                if (monthlyPayment.Value <= principal.Value * r)
                {
                    errors.Add("Monthly payment is too low to amortize this loan.");
                    return;
                }

                var n = -Math.Log(1d - (double)(principal.Value * r / monthlyPayment.Value)) / Math.Log((double)(1m + r));
                termMonths = (int)Math.Ceiling((decimal)n);
            }

            inferences.Add("Derived term from principal, payment, and rate.");
        }

        if (!monthlyPayment.HasValue && principal.HasValue && termMonths.HasValue)
        {
            var r = interestRate.Value / 12m;
            if (r == 0)
            {
                monthlyPayment = principal.Value / termMonths.Value;
            }
            else
            {
                var denominator = 1m - (decimal)Math.Pow((double)(1m + r), -termMonths.Value);
                if (denominator == 0)
                {
                    errors.Add("Unable to derive monthly payment with the provided values.");
                    return;
                }

                monthlyPayment = (principal.Value * r) / denominator;
            }

            inferences.Add("Derived monthly payment using amortization formula.");
        }

        if (!principal.HasValue)
        {
            errors.Add("Provide principal or total owed for amortizing-loan calculations.");
            return;
        }

        if (!termMonths.HasValue)
        {
            errors.Add("Provide term (months) or enough inputs to derive it for amortizing loans.");
            return;
        }

        if (!monthlyPayment.HasValue)
        {
            errors.Add("Provide monthly payment or enough inputs to derive it for amortizing loans.");
            return;
        }

        totalOwed = monthlyPayment.Value * termMonths.Value;
    }

    private static AmortisationScheduleResult BuildAmortisationSchedule(
        decimal principal,
        decimal? annualRate,
        decimal monthlyPayment,
        DateTime firstPaymentDate)
    {
        var monthlyRate = annualRate.HasValue ? annualRate.Value / 12m : 0m;
        var today = DateTime.Today;
        var rows = new List<AmortisationRow>();
        var balance = principal;

        for (var n = 1; balance > 0.005m && rows.Count < 1200; n++)
        {
            var paymentDate = firstPaymentDate.AddMonths(n - 1);
            var startingBalance = balance;
            var interest = decimal.Round(balance * monthlyRate, 2);
            var totalDue = balance + interest;
            var payment = decimal.Round(Math.Min(monthlyPayment, totalDue), 2);
            var principalPaid = decimal.Round(Math.Max(0m, payment - interest), 2);
            var endingBalance = decimal.Round(Math.Max(0m, startingBalance - principalPaid), 2);
            var isPaid = paymentDate.Date < today;

            rows.Add(new AmortisationRow
            {
                PaymentNumber = n,
                PaymentDate = paymentDate,
                StartingBalance = startingBalance,
                Payment = payment,
                InterestPortion = interest,
                PrincipalPortion = principalPaid,
                EndingBalance = endingBalance,
                IsPaid = isPaid
            });

            balance = endingBalance;
        }

        var paidRows = rows.Where(r => r.IsPaid).ToList();
        var paymentsMade = paidRows.Count;
        var totalInterestPaid = decimal.Round(paidRows.Sum(r => r.InterestPortion), 2);
        var totalPrincipalRepaid = decimal.Round(paidRows.Sum(r => r.PrincipalPortion), 2);
        var totalAmountPaid = decimal.Round(paidRows.Sum(r => r.Payment), 2);
        var currentRemainingBalance = paymentsMade > 0 ? paidRows[^1].EndingBalance : principal;
        var paymentsRemaining = rows.Count - paymentsMade;
        var payoffDate = rows.LastOrDefault()?.PaymentDate;
        var percentageCompleted = principal > 0
            ? decimal.Round(totalPrincipalRepaid / principal * 100m, 2)
            : 0m;

        return new AmortisationScheduleResult
        {
            Rows = rows,
            TotalInterestPaidToDate = totalInterestPaid,
            TotalPrincipalRepaidToDate = totalPrincipalRepaid,
            TotalAmountPaidToDate = totalAmountPaid,
            CurrentRemainingBalance = currentRemainingBalance,
            PaymentsMadeCount = paymentsMade,
            PaymentsRemainingCount = paymentsRemaining,
            PayoffDate = payoffDate,
            PercentageCompleted = percentageCompleted
        };
    }

    private static decimal? CalculateRemainingBalance(
        DebtInterestType type,
        decimal? principal,
        decimal? totalOwed,
        decimal? interestRate,
        decimal monthlyPayment,
        int paymentsMade,
        int? termMonths,
        DebtCompoundingFrequency? frequency)
    {
        if (paymentsMade < 0)
        {
            return null;
        }

        return type switch
        {
            DebtInterestType.Amortizing => CalculateAmortizingRemaining(principal, interestRate, monthlyPayment, paymentsMade),
            DebtInterestType.None => totalOwed.HasValue ? totalOwed.Value - (paymentsMade * monthlyPayment) : null,
            DebtInterestType.Simple => CalculateAmortizingRemaining(principal, interestRate, monthlyPayment, paymentsMade),
            DebtInterestType.Compound => CalculateCompoundRemaining(principal, interestRate, monthlyPayment, paymentsMade, frequency, totalOwed),
            _ => null
        };
    }

    private static decimal? CalculateAmortizingRemaining(decimal? principal, decimal? interestRate, decimal monthlyPayment, int paymentsMade)
    {
        if (!principal.HasValue || !interestRate.HasValue)
        {
            return null;
        }

        var r = interestRate.Value / 12m;
        if (r == 0)
        {
            return principal.Value - (paymentsMade * monthlyPayment);
        }

        var rDouble = (double)r;
        var pow = Math.Pow(1.0 + rDouble, paymentsMade);

        if (!double.IsFinite(pow))
            return null;

        var remainingDouble = ((double)principal.Value * pow) - ((double)monthlyPayment * (pow - 1.0) / rDouble);

        if (!double.IsFinite(remainingDouble) || remainingDouble > (double)decimal.MaxValue || remainingDouble < (double)decimal.MinValue)
            return null;

        return (decimal)Math.Round(remainingDouble, 10);
    }

    private static decimal? CalculateCompoundRemaining(
        decimal? principal,
        decimal? interestRate,
        decimal monthlyPayment,
        int paymentsMade,
        DebtCompoundingFrequency? frequency,
        decimal? totalOwed)
    {
        if (!principal.HasValue || !interestRate.HasValue)
        {
            return totalOwed.HasValue ? totalOwed.Value - (paymentsMade * monthlyPayment) : null;
        }

        var n = frequency == DebtCompoundingFrequency.Annual ? 1m : 12m;
        var monthlyRate = interestRate.Value / n;
        var balance = principal.Value;

        for (var i = 0; i < paymentsMade; i++)
        {
            balance += balance * monthlyRate;
            balance -= monthlyPayment;
        }

        return balance;
    }

    private static DebtInterestType ResolveInterestType(DebtInterestType? explicitType, decimal? interestRate)
    {
        if (explicitType.HasValue)
        {
            return explicitType.Value;
        }

        if (!interestRate.HasValue || interestRate <= 0)
        {
            return DebtInterestType.None;
        }

        return DebtInterestType.Amortizing;
    }

    private static decimal? NormalizeInterestRate(decimal? interestRate)
    {
        if (!interestRate.HasValue)
        {
            return null;
        }

        if (interestRate < 0)
        {
            return interestRate;
        }

        return interestRate > 1m ? interestRate / 100m : interestRate;
    }

    private static int? CalculateMonthSpan(DateTime start, DateTime end)
    {
        if (end <= start)
        {
            return null;
        }

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
            DebtInterestType.Simple => "total = payment × n, payment = (P × r) / (1 - (1 + r)^(-n))",
            DebtInterestType.Compound => "total = principal × (1 + rate / n)^(n × years)",
            DebtInterestType.Amortizing => "payment = (P × r) / (1 - (1 + r)^(-n))",
            DebtInterestType.None => "total = monthlyPayment × termMonths",
            _ => ""
        };
    }

    private static void ValidateBasicInput(DebtCalculationInput input, List<string> errors)
    {
        if (input.Principal.HasValue && input.Principal < 0)
        {
            errors.Add("Principal cannot be negative.");
        }

        if (input.MonthlyPayment.HasValue && input.MonthlyPayment <= 0)
        {
            errors.Add("Monthly payment must be greater than zero.");
        }

        if (input.TotalOwed.HasValue && input.TotalOwed <= 0)
        {
            errors.Add("Total owed must be greater than zero.");
        }

        if (input.InterestRate.HasValue && input.InterestRate < 0)
        {
            errors.Add("Interest rate cannot be negative.");
        }

        if (input.TermMonths.HasValue && input.TermMonths <= 0)
        {
            errors.Add("Term months must be greater than zero.");
        }

        if (input.StartDate.HasValue && input.EndDate.HasValue && input.EndDate <= input.StartDate)
        {
            errors.Add("End date must be after start date.");
        }

        if (input.PaymentsMade.HasValue && input.PaymentsMade < 0)
        {
            errors.Add("Payments made cannot be negative.");
        }
    }

    private static decimal? RoundCurrency(decimal? value)
    {
        return value.HasValue ? decimal.Round(value.Value, 2) : null;
    }
}
