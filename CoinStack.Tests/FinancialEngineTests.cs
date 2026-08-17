using CoinStack.Services;
using Xunit;

namespace CoinStack.Tests;

/// <summary>
/// Verification suite for <see cref="FinancialEngine"/>. Expected values are derived from
/// closed-form financial formulae and cross-checked against standard commercial rounding.
/// </summary>
public sealed class FinancialEngineTests
{
    // ---------------------------------------------------------------------
    // CalculateCompoundInterest
    // ---------------------------------------------------------------------

    [Fact]
    public void CalculateCompoundInterest_AnnualCompounding_MatchesClosedForm()
    {
        // 1000 * (1 + 0.05)^10 = 1628.894626777442... => 1628.89
        var result = FinancialEngine.CalculateCompoundInterest(1000m, 0.05m, 1, 10m);

        Assert.Equal(1628.89m, result);
    }

    [Fact]
    public void CalculateCompoundInterest_MonthlyCompounding_MatchesClosedForm()
    {
        // 10000 * (1 + 0.06/12)^(12*5) = 13488.5015...
        var result = FinancialEngine.CalculateCompoundInterest(10000m, 0.06m, 12, 5m);

        Assert.Equal(13488.50m, result);
    }

    [Fact]
    public void CalculateCompoundInterest_DailyCompounding_MatchesClosedForm()
    {
        // 5000 * (1 + 0.04/365)^(365*3) = 5637.4479...
        var result = FinancialEngine.CalculateCompoundInterest(5000m, 0.04m, 365, 3m);

        Assert.Equal(5637.45m, result);
    }

    [Fact]
    public void CalculateCompoundInterest_HigherFrequency_YieldsMoreThanLowerFrequency()
    {
        var annually = FinancialEngine.CalculateCompoundInterest(10000m, 0.08m, 1, 10m);
        var monthly = FinancialEngine.CalculateCompoundInterest(10000m, 0.08m, 12, 10m);
        var daily = FinancialEngine.CalculateCompoundInterest(10000m, 0.08m, 365, 10m);

        Assert.True(annually < monthly);
        Assert.True(monthly < daily);
    }

    [Fact]
    public void CalculateCompoundInterest_FractionalYears_IsSupported()
    {
        // 1000 * (1 + 0.12/12)^(12*0.5) = 1000 * 1.01^6 = 1061.520150601...
        var result = FinancialEngine.CalculateCompoundInterest(1000m, 0.12m, 12, 0.5m);

        Assert.Equal(1061.52m, result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(12)]
    [InlineData(365)]
    public void CalculateCompoundInterest_ZeroRate_ReturnsPrincipalUnchanged(int periodsPerYear)
    {
        var result = FinancialEngine.CalculateCompoundInterest(2500.75m, 0m, periodsPerYear, 15m);

        Assert.Equal(2500.75m, result);
    }

    [Fact]
    public void CalculateCompoundInterest_ZeroYears_ReturnsPrincipalUnchanged()
    {
        var result = FinancialEngine.CalculateCompoundInterest(2500.75m, 0.09m, 12, 0m);

        Assert.Equal(2500.75m, result);
    }

    [Fact]
    public void CalculateCompoundInterest_ZeroPrincipal_ReturnsZero()
    {
        var result = FinancialEngine.CalculateCompoundInterest(0m, 0.09m, 12, 10m);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateCompoundInterest_NegativePrincipal_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.CalculateCompoundInterest(-1m, 0.05m, 12, 5m));

        Assert.Equal("principal", ex.ParamName);
    }

    [Fact]
    public void CalculateCompoundInterest_NegativeRate_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.CalculateCompoundInterest(1000m, -0.01m, 12, 5m));

        Assert.Equal("annualRate", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-12)]
    public void CalculateCompoundInterest_NonPositiveCompoundingPeriods_Throws(int periodsPerYear)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.CalculateCompoundInterest(1000m, 0.05m, periodsPerYear, 5m));

        Assert.Equal("compoundingPeriodsPerYear", ex.ParamName);
    }

    [Fact]
    public void CalculateCompoundInterest_NegativeYears_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.CalculateCompoundInterest(1000m, 0.05m, 12, -1m));

        Assert.Equal("years", ex.ParamName);
    }

    [Fact]
    public void CalculateCompoundInterest_RateSuppliedAsPercentage_Throws()
    {
        // 750 would mean 75 000% - almost certainly a percentage/fraction mix-up.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.CalculateCompoundInterest(1000m, 750m, 12, 5m));

        Assert.Equal("annualRate", ex.ParamName);
    }

    // ---------------------------------------------------------------------
    // ProjectInflationAdjustedExpense
    // ---------------------------------------------------------------------

    [Fact]
    public void ProjectInflationAdjustedExpense_StandardCase_MatchesClosedForm()
    {
        // 1500 * (1.06)^10 = 2686.2716...
        var result = FinancialEngine.ProjectInflationAdjustedExpense(1500m, 0.06m, 10m);

        Assert.Equal(2686.27m, result);
    }

    [Fact]
    public void ProjectInflationAdjustedExpense_SingleYear_AppliesRateExactlyOnce()
    {
        var result = FinancialEngine.ProjectInflationAdjustedExpense(1000m, 0.055m, 1m);

        Assert.Equal(1055.00m, result);
    }

    [Fact]
    public void ProjectInflationAdjustedExpense_FractionalYears_IsSupported()
    {
        // 2000 * (1.08)^2.5 = 2424.3168...
        var result = FinancialEngine.ProjectInflationAdjustedExpense(2000m, 0.08m, 2.5m);

        Assert.Equal(2424.32m, result);
    }

    [Fact]
    public void ProjectInflationAdjustedExpense_ZeroInflation_ReturnsExpenseUnchanged()
    {
        var result = FinancialEngine.ProjectInflationAdjustedExpense(1234.56m, 0m, 30m);

        Assert.Equal(1234.56m, result);
    }

    [Fact]
    public void ProjectInflationAdjustedExpense_ZeroYears_ReturnsExpenseUnchanged()
    {
        var result = FinancialEngine.ProjectInflationAdjustedExpense(1234.56m, 0.07m, 0m);

        Assert.Equal(1234.56m, result);
    }

    [Fact]
    public void ProjectInflationAdjustedExpense_ZeroExpense_ReturnsZero()
    {
        var result = FinancialEngine.ProjectInflationAdjustedExpense(0m, 0.07m, 10m);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void ProjectInflationAdjustedExpense_IsMonotonicOverTime()
    {
        var year5 = FinancialEngine.ProjectInflationAdjustedExpense(1000m, 0.05m, 5m);
        var year10 = FinancialEngine.ProjectInflationAdjustedExpense(1000m, 0.05m, 10m);

        Assert.True(year10 > year5);
    }

    [Fact]
    public void ProjectInflationAdjustedExpense_NegativeExpense_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.ProjectInflationAdjustedExpense(-100m, 0.05m, 5m));

        Assert.Equal("currentMonthlyExpense", ex.ParamName);
    }

    [Fact]
    public void ProjectInflationAdjustedExpense_NegativeInflation_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.ProjectInflationAdjustedExpense(1000m, -0.02m, 5m));

        Assert.Equal("annualInflationRate", ex.ParamName);
    }

    [Fact]
    public void ProjectInflationAdjustedExpense_NegativeYears_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.ProjectInflationAdjustedExpense(1000m, 0.05m, -0.5m));

        Assert.Equal("years", ex.ParamName);
    }

    // ---------------------------------------------------------------------
    // CalculateSavingsProjection
    // ---------------------------------------------------------------------

    [Fact]
    public void CalculateSavingsProjection_InitialBalanceAndDeposits_MatchesClosedForm()
    {
        // r = 0.06/12 = 0.005, n = 120, (1.005)^120 = 1.81939673...
        // FV = 5000 * 1.81939673 + 500 * ((1.81939673 - 1)/0.005) = 91036.66
        var result = FinancialEngine.CalculateSavingsProjection(5000m, 500m, 0.06m, 120);

        Assert.Equal(91036.66m, result.FutureValue);
        Assert.Equal(65000.00m, result.TotalContributions);
        Assert.Equal(26036.66m, result.TotalInterestEarned);
        Assert.Equal(120, result.Months);
    }

    [Fact]
    public void CalculateSavingsProjection_ZeroInitialBalance_UsesPureAnnuity()
    {
        // 250 * (((1 + 0.07/12)^24 - 1) / (0.07/12)) = 6420.2609...
        var result = FinancialEngine.CalculateSavingsProjection(0m, 250m, 0.07m, 24);

        Assert.Equal(6420.26m, result.FutureValue);
        Assert.Equal(6000.00m, result.TotalContributions);
        Assert.Equal(420.26m, result.TotalInterestEarned);
    }

    [Fact]
    public void CalculateSavingsProjection_ZeroMonthlyDeposit_BehavesLikeLumpSumCompounding()
    {
        var projection = FinancialEngine.CalculateSavingsProjection(10000m, 0m, 0.06m, 60);
        var lumpSum = FinancialEngine.CalculateCompoundInterest(10000m, 0.06m, 12, 5m);

        Assert.Equal(lumpSum, projection.FutureValue);
        Assert.Equal(10000.00m, projection.TotalContributions);
    }

    [Fact]
    public void CalculateSavingsProjection_ZeroInitialAndZeroDeposit_ReturnsZero()
    {
        var result = FinancialEngine.CalculateSavingsProjection(0m, 0m, 0.07m, 60);

        Assert.Equal(0m, result.FutureValue);
        Assert.Equal(0m, result.TotalContributions);
        Assert.Equal(0m, result.TotalInterestEarned);
    }

    [Fact]
    public void CalculateSavingsProjection_ZeroRate_SumsContributionsWithoutInterest()
    {
        var result = FinancialEngine.CalculateSavingsProjection(1000m, 200m, 0m, 36);

        Assert.Equal(8200.00m, result.FutureValue);
        Assert.Equal(8200.00m, result.TotalContributions);
        Assert.Equal(0m, result.TotalInterestEarned);
    }

    [Fact]
    public void CalculateSavingsProjection_ZeroMonths_ReturnsInitialBalance()
    {
        var result = FinancialEngine.CalculateSavingsProjection(4321.99m, 500m, 0.08m, 0);

        Assert.Equal(4321.99m, result.FutureValue);
        Assert.Equal(4321.99m, result.TotalContributions);
        Assert.Equal(0m, result.TotalInterestEarned);
        Assert.Equal(0, result.Months);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(60)]
    [InlineData(240)]
    [InlineData(480)]
    public void CalculateSavingsProjection_WithPositiveRate_AlwaysBeatsContributions(int months)
    {
        var result = FinancialEngine.CalculateSavingsProjection(1000m, 100m, 0.05m, months);

        Assert.True(result.FutureValue > result.TotalContributions);
        Assert.True(result.TotalInterestEarned > 0m);
    }

    [Fact]
    public void CalculateSavingsProjection_InterestEarned_EqualsFutureValueMinusContributions()
    {
        var result = FinancialEngine.CalculateSavingsProjection(7500m, 325.50m, 0.0925m, 187);

        Assert.Equal(result.FutureValue - result.TotalContributions, result.TotalInterestEarned);
    }

    [Fact]
    public void CalculateSavingsProjection_NegativeInitialBalance_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.CalculateSavingsProjection(-1m, 100m, 0.05m, 12));

        Assert.Equal("initialBalance", ex.ParamName);
    }

    [Fact]
    public void CalculateSavingsProjection_NegativeMonthlyDeposit_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.CalculateSavingsProjection(1000m, -100m, 0.05m, 12));

        Assert.Equal("monthlyDeposit", ex.ParamName);
    }

    [Fact]
    public void CalculateSavingsProjection_NegativeRate_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.CalculateSavingsProjection(1000m, 100m, -0.05m, 12));

        Assert.Equal("annualRate", ex.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-12)]
    public void CalculateSavingsProjection_NegativeMonths_Throws(int months)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.CalculateSavingsProjection(1000m, 100m, 0.05m, months));

        Assert.Equal("months", ex.ParamName);
    }

    // ---------------------------------------------------------------------
    // CalculateLoanPayment
    // ---------------------------------------------------------------------

    [Fact]
    public void CalculateLoanPayment_StandardMortgage_MatchesClosedForm()
    {
        // P = 250000, r = 0.115/12, n = 240 => payment = 2666.0699... => 2666.07
        var result = FinancialEngine.CalculateLoanPayment(250000m, 0.115m, 240);

        Assert.Equal(2666.07m, result.MonthlyPayment);
        Assert.Equal(639856.80m, result.TotalPayment);
        Assert.Equal(389856.80m, result.TotalInterest);
        Assert.Equal(250000.00m, result.Principal);
        Assert.Equal(240, result.TermMonths);
        Assert.Equal(0.115m, result.AnnualInterestRate);
    }

    [Fact]
    public void CalculateLoanPayment_VehicleFinance_MatchesClosedForm()
    {
        // P = 350000, r = 0.1275/12, n = 72 => payment = 6979.8358... => 6979.84
        var result = FinancialEngine.CalculateLoanPayment(350000m, 0.1275m, 72);

        Assert.Equal(6979.84m, result.MonthlyPayment);
        Assert.Equal(502548.48m, result.TotalPayment);
        Assert.Equal(152548.48m, result.TotalInterest);
    }

    [Fact]
    public void CalculateLoanPayment_ZeroRate_RepaysStraightLine()
    {
        var result = FinancialEngine.CalculateLoanPayment(12000m, 0m, 24);

        Assert.Equal(500.00m, result.MonthlyPayment);
        Assert.Equal(12000.00m, result.TotalPayment);
        Assert.Equal(0m, result.TotalInterest);
    }

    [Fact]
    public void CalculateLoanPayment_SingleMonthTerm_ChargesOneMonthOfInterest()
    {
        // One instalment settles principal plus a single month of interest.
        var result = FinancialEngine.CalculateLoanPayment(1000m, 0.12m, 1);

        Assert.Equal(1010.00m, result.MonthlyPayment);
        Assert.Equal(10.00m, result.TotalInterest);
    }

    [Fact]
    public void CalculateLoanPayment_LongerTerm_LowersInstalmentButRaisesTotalInterest()
    {
        var shortTerm = FinancialEngine.CalculateLoanPayment(200000m, 0.1m, 120);
        var longTerm = FinancialEngine.CalculateLoanPayment(200000m, 0.1m, 240);

        Assert.True(longTerm.MonthlyPayment < shortTerm.MonthlyPayment);
        Assert.True(longTerm.TotalInterest > shortTerm.TotalInterest);
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.075)]
    [InlineData(0.1125)]
    [InlineData(0.185)]
    public void CalculateLoanPayment_TotalsAreInternallyConsistent(decimal annualRate)
    {
        var result = FinancialEngine.CalculateLoanPayment(150000m, annualRate, 60);

        Assert.Equal(result.MonthlyPayment * result.TermMonths, result.TotalPayment);
        Assert.Equal(result.TotalPayment - result.Principal, result.TotalInterest);
        Assert.True(result.MonthlyPayment > 0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-25000)]
    public void CalculateLoanPayment_NonPositivePrincipal_Throws(decimal principal)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.CalculateLoanPayment(principal, 0.1m, 60));

        Assert.Equal("principal", ex.ParamName);
    }

    [Fact]
    public void CalculateLoanPayment_NegativeRate_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.CalculateLoanPayment(100000m, -0.01m, 60));

        Assert.Equal("annualRate", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-60)]
    public void CalculateLoanPayment_NonPositiveTerm_Throws(int termMonths)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.CalculateLoanPayment(100000m, 0.1m, termMonths));

        Assert.Equal("termMonths", ex.ParamName);
    }

    // ---------------------------------------------------------------------
    // Rounding, precision and determinism guarantees
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(2.345, 2.35)]
    [InlineData(2.344, 2.34)]
    [InlineData(-2.345, -2.35)]
    [InlineData(0.005, 0.01)]
    [InlineData(1.005, 1.01)]
    [InlineData(2.675, 2.68)]
    public void RoundCurrency_UsesCommercialRoundingAwayFromZero(decimal input, decimal expected)
    {
        Assert.Equal(expected, FinancialEngine.RoundCurrency(input));
    }

    [Fact]
    public void RoundCurrency_DoesNotUseBankersRounding()
    {
        // Banker's rounding would give 2.34 and 2.36 respectively.
        Assert.Equal(2.35m, FinancialEngine.RoundCurrency(2.345m));
        Assert.Equal(2.36m, FinancialEngine.RoundCurrency(2.355m));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(12)]
    [InlineData(365)]
    public void AllCurrencyOutputs_HaveAtMostTwoDecimalPlaces(int periodsPerYear)
    {
        var compound = FinancialEngine.CalculateCompoundInterest(1234.567m, 0.0789m, periodsPerYear, 7m);
        var inflation = FinancialEngine.ProjectInflationAdjustedExpense(987.654m, 0.0611m, 7m);
        var savings = FinancialEngine.CalculateSavingsProjection(1234.56m, 321.99m, 0.0733m, 89);
        var loan = FinancialEngine.CalculateLoanPayment(123456.78m, 0.1234m, 77);

        AssertAtMostTwoDecimals(compound);
        AssertAtMostTwoDecimals(inflation);
        AssertAtMostTwoDecimals(savings.FutureValue);
        AssertAtMostTwoDecimals(savings.TotalContributions);
        AssertAtMostTwoDecimals(savings.TotalInterestEarned);
        AssertAtMostTwoDecimals(loan.MonthlyPayment);
        AssertAtMostTwoDecimals(loan.TotalPayment);
        AssertAtMostTwoDecimals(loan.TotalInterest);
    }

    [Fact]
    public void Pow_WithWholeExponent_IsExactAndBeatsDoubleArithmetic()
    {
        // (1.1)^3 is exactly 1.331. Double arithmetic drifts here; decimal must not.
        Assert.Equal(1.331m, FinancialEngine.Pow(1.1m, 3m));
        Assert.Equal(1.02m * 1.02m, FinancialEngine.Pow(1.02m, 2m));
    }

    [Fact]
    public void Pow_WithNegativeExponent_ReturnsReciprocal()
    {
        var positive = FinancialEngine.Pow(1.05m, 12m);
        var negative = FinancialEngine.Pow(1.05m, -12m);

        Assert.True(Math.Abs((positive * negative) - 1m) < 0.0000000001m);
    }

    [Fact]
    public void Pow_WithZeroExponent_ReturnsOne()
    {
        Assert.Equal(1m, FinancialEngine.Pow(1.075m, 0m));
    }

    [Fact]
    public void Pow_WithFractionalExponent_IsAccurate()
    {
        // 1.21^0.5 = 1.1
        var result = FinancialEngine.Pow(1.21m, 0.5m);

        Assert.True(Math.Abs(result - 1.1m) < 0.0000000001m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Pow_WithNonPositiveBase_Throws(decimal baseValue)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.Pow(baseValue, 2m));

        Assert.Equal("baseValue", ex.ParamName);
    }

    [Fact]
    public void Ln_AndExp_AreMutualInverses()
    {
        var value = 1.0575m;
        var roundTrip = FinancialEngine.Exp(FinancialEngine.Ln(value));

        Assert.True(Math.Abs(roundTrip - value) < 0.0000000001m);
    }

    [Fact]
    public void Ln_OfOne_IsZero()
    {
        Assert.Equal(0m, FinancialEngine.Ln(1m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Ln_OfNonPositiveValue_Throws(decimal value)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => FinancialEngine.Ln(value));

        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void Exp_OfZero_IsOne()
    {
        Assert.Equal(1m, FinancialEngine.Exp(0m));
    }

    [Fact]
    public void Engine_IsDeterministic_AcrossRepeatedInvocations()
    {
        var first = FinancialEngine.CalculateLoanPayment(250000m, 0.115m, 240);

        for (var i = 0; i < 250; i++)
        {
            Assert.Equal(first, FinancialEngine.CalculateLoanPayment(250000m, 0.115m, 240));
        }
    }

    [Fact]
    public void Engine_IsThreadSafe_UnderParallelLoad()
    {
        var expectedLoan = FinancialEngine.CalculateLoanPayment(250000m, 0.115m, 240);
        var expectedSavings = FinancialEngine.CalculateSavingsProjection(5000m, 500m, 0.06m, 120);
        var expectedCompound = FinancialEngine.CalculateCompoundInterest(10000m, 0.06m, 12, 5m);

        Parallel.For(0, 2000, _ =>
        {
            Assert.Equal(expectedLoan, FinancialEngine.CalculateLoanPayment(250000m, 0.115m, 240));
            Assert.Equal(expectedSavings, FinancialEngine.CalculateSavingsProjection(5000m, 500m, 0.06m, 120));
            Assert.Equal(expectedCompound, FinancialEngine.CalculateCompoundInterest(10000m, 0.06m, 12, 5m));
        });
    }

    // ---------------------------------------------------------------------
    // PercentToFraction
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7.5, 0.075)]
    [InlineData(11.5, 0.115)]
    [InlineData(100, 1)]
    [InlineData(0.5, 0.005)]
    public void PercentToFraction_ConvertsCorrectly(decimal percent, decimal expected)
    {
        Assert.Equal(expected, FinancialEngine.PercentToFraction(percent));
    }

    [Fact]
    public void PercentToFraction_NegativePercent_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.PercentToFraction(-1m));

        Assert.Equal("percent", ex.ParamName);
    }

    [Fact]
    public void PercentToFraction_DistinguishesSmallRatesFromLargeOnes()
    {
        // The old magnitude-guessing heuristic could not tell 0.5% from 50%.
        Assert.Equal(0.005m, FinancialEngine.PercentToFraction(0.5m));
        Assert.Equal(0.5m, FinancialEngine.PercentToFraction(50m));
    }

    // ---------------------------------------------------------------------
    // BuildAmortisationSchedule
    // ---------------------------------------------------------------------

    [Fact]
    public void BuildAmortisationSchedule_ClearsBalanceExactly()
    {
        var schedule = FinancialEngine.BuildAmortisationSchedule(10000m, 0.12m, 500m);

        Assert.False(schedule.IsUnpayable);
        Assert.NotNull(schedule.MonthsToPayoff);
        Assert.Equal(0m, schedule.Instalments[^1].ClosingBalance);
        Assert.Equal(schedule.Instalments.Count, schedule.MonthsToPayoff);
    }

    [Fact]
    public void BuildAmortisationSchedule_PrincipalPortionsSumToOriginalBalance()
    {
        var schedule = FinancialEngine.BuildAmortisationSchedule(25000m, 0.095m, 800m);

        var totalPrincipal = 0m;
        foreach (var instalment in schedule.Instalments)
        {
            totalPrincipal += instalment.PrincipalPortion;
        }

        Assert.Equal(25000m, totalPrincipal);
    }

    [Fact]
    public void BuildAmortisationSchedule_TotalsAreConsistent()
    {
        var schedule = FinancialEngine.BuildAmortisationSchedule(15000m, 0.18m, 600m);

        var summedPayments = 0m;
        var summedInterest = 0m;
        foreach (var instalment in schedule.Instalments)
        {
            summedPayments += instalment.Payment;
            summedInterest += instalment.InterestPortion;
        }

        Assert.Equal(summedPayments, schedule.TotalPaid);
        Assert.Equal(summedInterest, schedule.TotalInterest);
        Assert.Equal(FinancialEngine.RoundCurrency(15000m + summedInterest), schedule.TotalPaid);
    }

    [Fact]
    public void BuildAmortisationSchedule_BalancesChainBetweenRows()
    {
        var schedule = FinancialEngine.BuildAmortisationSchedule(9000m, 0.1m, 400m);

        for (var i = 1; i < schedule.Instalments.Count; i++)
        {
            Assert.Equal(schedule.Instalments[i - 1].ClosingBalance, schedule.Instalments[i].OpeningBalance);
        }
    }

    [Fact]
    public void BuildAmortisationSchedule_PaymentBelowMonthlyInterest_IsFlaggedUnpayable()
    {
        // 1% monthly interest on 10 000 is 100; a 50 payment can never clear the debt.
        var schedule = FinancialEngine.BuildAmortisationSchedule(10000m, 0.12m, 50m);

        Assert.True(schedule.IsUnpayable);
        Assert.Null(schedule.MonthsToPayoff);
        Assert.Empty(schedule.Instalments);
    }

    [Fact]
    public void BuildAmortisationSchedule_ZeroRate_IsPureStraightLine()
    {
        var schedule = FinancialEngine.BuildAmortisationSchedule(1200m, 0m, 100m);

        Assert.Equal(12, schedule.MonthsToPayoff);
        Assert.Equal(0m, schedule.TotalInterest);
        Assert.Equal(1200m, schedule.TotalPaid);
    }

    [Fact]
    public void BuildAmortisationSchedule_ZeroBalance_ReturnsEmptySchedule()
    {
        var schedule = FinancialEngine.BuildAmortisationSchedule(0m, 0.1m, 500m);

        Assert.Empty(schedule.Instalments);
        Assert.Equal(0, schedule.MonthsToPayoff);
        Assert.False(schedule.IsUnpayable);
    }

    [Fact]
    public void BuildAmortisationSchedule_FinalInstalmentDoesNotOvershoot()
    {
        var schedule = FinancialEngine.BuildAmortisationSchedule(5000m, 0.15m, 777m);
        var last = schedule.Instalments[^1];

        Assert.True(last.Payment <= 777m);
        Assert.Equal(0m, last.ClosingBalance);
    }

    [Fact]
    public void BuildAmortisationSchedule_AgreesWithCalculateLoanPaymentOnTermLength()
    {
        // Paying exactly the contractual instalment should retire the loan on schedule.
        var loan = FinancialEngine.CalculateLoanPayment(200000m, 0.105m, 120);
        var schedule = FinancialEngine.BuildAmortisationSchedule(200000m, 0.105m, loan.MonthlyPayment);

        Assert.Equal(120, schedule.MonthsToPayoff);
    }

    [Fact]
    public void BuildAmortisationSchedule_HigherPayment_ReducesInterestAndTerm()
    {
        var lower = FinancialEngine.BuildAmortisationSchedule(20000m, 0.14m, 500m);
        var higher = FinancialEngine.BuildAmortisationSchedule(20000m, 0.14m, 900m);

        Assert.True(higher.MonthsToPayoff < lower.MonthsToPayoff);
        Assert.True(higher.TotalInterest < lower.TotalInterest);
    }

    [Fact]
    public void BuildAmortisationSchedule_RespectsMaxMonthsCap()
    {
        var schedule = FinancialEngine.BuildAmortisationSchedule(100000m, 0.2m, 1700m, maxMonths: 12);

        Assert.Equal(12, schedule.Instalments.Count);
        Assert.True(schedule.IsUnpayable);
        Assert.Null(schedule.MonthsToPayoff);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void BuildAmortisationSchedule_NonPositivePayment_Throws(decimal monthlyPayment)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.BuildAmortisationSchedule(10000m, 0.1m, monthlyPayment));

        Assert.Equal("monthlyPayment", ex.ParamName);
    }

    [Fact]
    public void BuildAmortisationSchedule_NegativeBalance_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.BuildAmortisationSchedule(-1m, 0.1m, 500m));

        Assert.Equal("balance", ex.ParamName);
    }

    [Fact]
    public void BuildAmortisationSchedule_NonPositiveMaxMonths_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => FinancialEngine.BuildAmortisationSchedule(10000m, 0.1m, 500m, maxMonths: 0));

        Assert.Equal("maxMonths", ex.ParamName);
    }

    /// <summary>
    /// Asserts that a monetary value carries no more than two decimal places.
    /// </summary>
    /// <param name="value">The value under test.</param>
    private static void AssertAtMostTwoDecimals(decimal value)
    {
        Assert.Equal(value, decimal.Round(value, 2, MidpointRounding.AwayFromZero));
    }
}
