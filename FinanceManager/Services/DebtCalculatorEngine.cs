namespace FinanceManager.Services;

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
            FormulaUsed = GetFormulaLabel(resolvedType)
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
        if (!years.HasValue && termMonths.HasValue)
        {
            years = termMonths.Value / 12m;
        }

        if (!years.HasValue && monthlyPayment.HasValue && totalOwed.HasValue && monthlyPayment > 0)
        {
            termMonths = (int)Math.Ceiling(totalOwed.Value / monthlyPayment.Value);
            years = termMonths.Value / 12m;
            inferences.Add("Derived term from total owed and monthly payment.");
        }

        if (!interestRate.HasValue)
        {
            errors.Add("Interest rate is required for simple-interest debt.");
            return;
        }

        if (!years.HasValue || years <= 0)
        {
            if (principal.HasValue && monthlyPayment.HasValue)
            {
                var denominator = monthlyPayment.Value - (principal.Value * interestRate.Value / 12m);
                if (denominator <= 0)
                {
                    errors.Add("Monthly payment is too low to offset monthly simple interest.");
                    return;
                }

                var inferredTerm = principal.Value / denominator;
                termMonths = (int)Math.Ceiling(inferredTerm);
                years = termMonths.Value / 12m;
                inferences.Add("Derived term from principal, interest rate, and monthly payment.");
            }
            else
            {
                errors.Add("Provide term (months) or start/end dates for simple-interest calculations.");
                return;
            }
        }

        var factor = 1m + (interestRate.Value * years.Value);

        if (!principal.HasValue && totalOwed.HasValue)
        {
            if (factor <= 0)
            {
                errors.Add("Cannot derive principal with the provided interest inputs.");
                return;
            }

            principal = totalOwed.Value / factor;
            inferences.Add("Derived principal from total owed and simple-interest factor.");
        }

        if (!totalOwed.HasValue && principal.HasValue)
        {
            totalOwed = principal.Value * factor;
            inferences.Add("Derived total owed using simple-interest formula.");
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
            DebtInterestType.Simple => totalOwed.HasValue ? totalOwed.Value - (paymentsMade * monthlyPayment) : null,
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

        var pow = Math.Round((decimal)Math.Pow((double)(1m + r), paymentsMade), 3);
        var remaining = (principal.Value * pow) - (monthlyPayment * (pow - 1m) / r);
        return remaining;
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
            DebtInterestType.Simple => "total = principal × (1 + rate × years)",
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
