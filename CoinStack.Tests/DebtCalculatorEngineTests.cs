using CoinStack.Services;
using Xunit;

namespace CoinStack.Tests;

public sealed class DebtCalculatorEngineTests
{
    private readonly DebtCalculatorEngine _engine = new();

    [Fact]
    public void Simple_Interest_Is_Principal_Times_Rate_Times_Years()
    {
        var result = _engine.Calculate(new DebtCalculationInput
        {
            Principal = 1000m,
            AnnualRatePercent = 10m,
            InterestType = DebtInterestType.Simple,
            TermMonths = 36
        });

        Assert.True(result.IsValid);
        Assert.Equal(300m, result.InterestAmount);
        Assert.Equal(1300m, result.TotalOwed);
        Assert.Equal(36.11m, result.MonthlyPayment);
    }

    [Fact]
    public void Simple_Interest_Derives_Term_From_Payment()
    {
        var result = _engine.Calculate(new DebtCalculationInput
        {
            Principal = 1000m,
            AnnualRatePercent = 10m,
            InterestType = DebtInterestType.Simple,
            MonthlyPayment = 36.12m
        });

        Assert.True(result.IsValid);
        Assert.Equal(36, result.TermMonths);
    }

    [Fact]
    public void Rate_Of_One_Means_One_Percent()
    {
        var result = _engine.Calculate(new DebtCalculationInput
        {
            Principal = 1200m,
            AnnualRatePercent = 1m,
            InterestType = DebtInterestType.Amortizing,
            TermMonths = 12
        });

        Assert.True(result.IsValid);
        Assert.Equal(0.01m, result.InterestRate);
        Assert.InRange(result.MonthlyPayment!.Value, 100.50m, 100.60m);
    }

    [Fact]
    public void Annual_Compounding_Charges_A_Twelfth_Root_Each_Month_Not_The_Full_Year()
    {
        var result = _engine.Calculate(new DebtCalculationInput
        {
            Principal = 12000m,
            AnnualRatePercent = 12m,
            InterestType = DebtInterestType.Compound,
            CompoundingFrequency = DebtCompoundingFrequency.Annual,
            TermMonths = 12
        });

        Assert.True(result.IsValid);
        Assert.InRange(result.EffectiveMonthlyRate!.Value, 0.009488m, 0.009490m);

        var monthly = _engine.Calculate(new DebtCalculationInput
        {
            Principal = 12000m,
            AnnualRatePercent = 12m,
            InterestType = DebtInterestType.Compound,
            CompoundingFrequency = DebtCompoundingFrequency.Monthly,
            TermMonths = 12
        });

        // Once-a-year compounding costs a little less than monthly compounding at the same nominal rate.
        Assert.True(result.MonthlyPayment < monthly.MonthlyPayment);
    }

    [Fact]
    public void Annual_Compounding_Remaining_Balance_Is_Not_Charged_The_Yearly_Rate_Monthly()
    {
        var result = _engine.Calculate(new DebtCalculationInput
        {
            Principal = 10000m,
            AnnualRatePercent = 12m,
            InterestType = DebtInterestType.Compound,
            CompoundingFrequency = DebtCompoundingFrequency.Annual,
            MonthlyPayment = 500m,
            PaymentsMade = 1
        });

        // One month at the 12%-a-year equivalent adds about R94.89, not R1 200.
        Assert.InRange(result.RemainingBalanceAfterPayments!.Value, 9594m, 9595m);
    }

    [Fact]
    public void Compound_Debt_With_Payments_Is_Amortised_Not_A_Lump_Sum_Split_Over_The_Term()
    {
        var result = _engine.Calculate(new DebtCalculationInput
        {
            Principal = 1000m,
            AnnualRatePercent = 10m,
            InterestType = DebtInterestType.Compound,
            CompoundingFrequency = DebtCompoundingFrequency.Monthly,
            TermMonths = 36
        });

        Assert.True(result.IsValid);
        Assert.Equal(32.27m, result.MonthlyPayment);
        Assert.InRange(result.TotalOwed!.Value, 1161.60m, 1161.63m);
    }

    [Fact]
    public void Total_Owed_Stops_When_The_Debt_Is_Repaid_Early()
    {
        var result = _engine.Calculate(new DebtCalculationInput
        {
            Principal = 10000m,
            AnnualRatePercent = 10m,
            InterestType = DebtInterestType.Amortizing,
            MonthlyPayment = 1000m,
            TermMonths = 36
        });

        Assert.True(result.IsValid);
        Assert.Equal(11, result.TermMonths);
        Assert.InRange(result.TotalOwed!.Value, 10400m, 10500m);
        Assert.Contains(result.Warnings, w => w.Contains("repaid after 11 months"));
    }

    [Fact]
    public void Derived_Term_Total_Uses_A_Smaller_Final_Payment()
    {
        var result = _engine.Calculate(new DebtCalculationInput
        {
            Principal = 1000m,
            AnnualRatePercent = 0m,
            InterestType = DebtInterestType.Amortizing,
            MonthlyPayment = 300m
        });

        Assert.Equal(4, result.TermMonths);
        Assert.Equal(1000m, result.TotalOwed);
        Assert.Equal(0m, result.InterestAmount);
    }

    [Fact]
    public void Apr_Compounds_Daily_And_Costs_More_Than_The_Same_Fixed_Rate()
    {
        var apr = _engine.Calculate(new DebtCalculationInput
        {
            Principal = 5000m,
            AnnualRatePercent = 24m,
            InterestType = DebtInterestType.Amortizing,
            CompoundingFrequency = DebtCompoundingFrequency.Daily,
            TermMonths = 24
        });

        var fixedRate = _engine.Calculate(new DebtCalculationInput
        {
            Principal = 5000m,
            AnnualRatePercent = 24m,
            InterestType = DebtInterestType.Amortizing,
            TermMonths = 24
        });

        Assert.InRange(apr.EffectiveMonthlyRate!.Value, 0.0201m, 0.0202m);
        Assert.True(apr.MonthlyPayment > fixedRate.MonthlyPayment);
    }

    [Fact]
    public void Variable_Rate_Shows_What_The_Payment_Becomes_If_The_Rate_Moves()
    {
        var result = _engine.Calculate(new DebtCalculationInput
        {
            Principal = 100000m,
            AnnualRatePercent = 11m,
            InterestType = DebtInterestType.Amortizing,
            IsVariableRate = true,
            TermMonths = 180
        });

        Assert.Equal(3, result.RateScenarios.Count);
        var up = Assert.Single(result.RateScenarios, s => s.RateChangePercentPoints == 1m);
        Assert.Equal(0.12m, up.AnnualRate);
        Assert.True(up.MonthlyPayment > result.MonthlyPayment);

        var fixedResult = _engine.Calculate(new DebtCalculationInput
        {
            Principal = 100000m,
            AnnualRatePercent = 11m,
            InterestType = DebtInterestType.Amortizing,
            TermMonths = 180
        });
        Assert.Empty(fixedResult.RateScenarios);
    }

    [Fact]
    public void No_Interest_Debt_Totals_Payment_Times_Term()
    {
        var result = _engine.Calculate(new DebtCalculationInput
        {
            MonthlyPayment = 600m,
            TermMonths = 36,
            InterestType = DebtInterestType.None
        });

        Assert.True(result.IsValid);
        Assert.Equal(21600m, result.TotalOwed);
    }

    [Fact]
    public void Payment_Below_The_Monthly_Interest_Is_An_Error_Not_A_Crash()
    {
        var result = _engine.Calculate(new DebtCalculationInput
        {
            Principal = 900_000_000_000m,
            AnnualRatePercent = 1000m,
            InterestType = DebtInterestType.Amortizing,
            MonthlyPayment = 1m,
            TermMonths = 1200
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("too low"));
    }

    [Theory]
    [InlineData("1000000000000000")]
    [InlineData("79000000000000000000000000000")]
    public void Huge_Amounts_Are_Rejected(string principal)
    {
        var result = _engine.Calculate(new DebtCalculationInput
        {
            Principal = decimal.Parse(principal, System.Globalization.CultureInfo.InvariantCulture),
            AnnualRatePercent = 20m,
            InterestType = DebtInterestType.Compound,
            TermMonths = 600
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Largest_Allowed_Inputs_Still_Calculate()
    {
        var result = _engine.Calculate(new DebtCalculationInput
        {
            Principal = DebtCalculatorEngine.MaxAmount,
            AnnualRatePercent = DebtCalculatorEngine.MaxAnnualRatePercent,
            InterestType = DebtInterestType.Compound,
            CompoundingFrequency = DebtCompoundingFrequency.Daily,
            TermMonths = DebtCalculatorEngine.MaxTermMonths,
            PaymentsMade = 600,
            IsVariableRate = true
        });

        // The point is that this returns instead of throwing an OverflowException up to the page.
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Remaining_Balance_After_Payments_With_No_Interest()
    {
        var result = _engine.Calculate(new DebtCalculationInput
        {
            Principal = 1200m,
            AnnualRatePercent = 0m,
            InterestType = DebtInterestType.Amortizing,
            MonthlyPayment = 100m,
            PaymentsMade = 3
        });

        Assert.Equal(900m, result.RemainingBalanceAfterPayments);
    }
}
