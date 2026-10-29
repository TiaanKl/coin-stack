namespace CoinStack.Services;

public interface IDebtCalculatorEngine
{
    DebtCalculationResult Calculate(DebtCalculationInput input);
}
