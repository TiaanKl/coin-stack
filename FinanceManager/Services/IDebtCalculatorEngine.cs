namespace FinanceManager.Services;

public interface IDebtCalculatorEngine
{
    DebtCalculationResult Calculate(DebtCalculationInput input);
}
