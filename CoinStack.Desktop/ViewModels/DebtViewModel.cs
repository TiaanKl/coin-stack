using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoinStack.Desktop.ViewModels;

public partial class DebtViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IDebtService _debts;
    private readonly IDebtCalculatorEngine _calculator;
    private readonly ISettingsService _settings;
    private readonly IMoneyFormatter _money;

    private string _currency = "USD";

    [ObservableProperty] private string _totalOwedText = "";
    [ObservableProperty] private string _monthlyPaymentTotalText = "";
    [ObservableProperty] private int _openDebtCount;
    [ObservableProperty] private bool _hasDebts;

    // Calculator
    [ObservableProperty] private decimal _calcPrincipal = 10000m;
    [ObservableProperty] private decimal _calcInterestRate = 12m;
    [ObservableProperty] private int _calcTermMonths = 36;
    [ObservableProperty] private string _calcPaymentText = "—";
    [ObservableProperty] private string _calcTotalText = "—";
    [ObservableProperty] private string _calcInterestText = "—";
    [ObservableProperty] private string _calcSummary = "Enter a balance, rate, and term, then calculate.";

    // New debt form
    [ObservableProperty] private string _newDebtName = "";
    [ObservableProperty] private string _newProvider = "";
    [ObservableProperty] private decimal _newBalance;
    [ObservableProperty] private decimal _newTotal;
    [ObservableProperty] private decimal _newInterestRate;
    [ObservableProperty] private decimal _newMonthlyPayment;

    public ObservableCollection<DebtRow> Debts { get; } = [];

    public DebtViewModel(
        IDebtService debts,
        IDebtCalculatorEngine calculator,
        ISettingsService settings,
        IMoneyFormatter money)
    {
        _debts = debts;
        _calculator = calculator;
        _settings = settings;
        _money = money;
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var settings = await _settings.GetAsync();
            _currency = settings.Currency;

            var debts = await _debts.GetAllAsync();
            var open = debts.Where(d => d.CurrentBalance > 0).ToList();

            OpenDebtCount = open.Count;
            TotalOwedText = _money.Format(_currency, open.Sum(d => d.CurrentBalance));
            MonthlyPaymentTotalText = _money.Format(_currency, open.Sum(d => d.MonthlyPaymentAmount));

            Debts.Clear();
            foreach (var debt in debts.OrderByDescending(d => d.CurrentBalance))
            {
                var total = debt.TotalAmount <= 0 ? Math.Max(debt.CurrentBalance, 1m) : debt.TotalAmount;
                var paid = Math.Max(0m, total - debt.CurrentBalance);
                var pct = total > 0 ? Math.Min(100, (double)(paid / total * 100m)) : 0;

                Debts.Add(new DebtRow
                {
                    Id = debt.Id,
                    Name = debt.Name,
                    Provider = string.IsNullOrWhiteSpace(debt.Provider) ? "No provider" : debt.Provider!,
                    BalanceText = _money.Format(_currency, debt.CurrentBalance),
                    TotalText = _money.Format(_currency, debt.TotalAmount),
                    InterestText = $"{debt.InterestRatePercent:0.##}% APR",
                    PaymentText = _money.Format(_currency, debt.MonthlyPaymentAmount),
                    ProgressValue = (double)paid,
                    ProgressMaximum = (double)total,
                    ProgressPercentText = $"{pct:0}% paid"
                });
            }

            HasDebts = Debts.Count > 0;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load debts: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Calculate()
    {
        if (CalcPrincipal <= 0 || CalcTermMonths <= 0)
        {
            CalcSummary = "Principal and term must be greater than zero.";
            CalcPaymentText = CalcTotalText = CalcInterestText = "—";
            return;
        }

        var result = _calculator.Calculate(new DebtCalculationInput
        {
            Principal = CalcPrincipal,
            InterestRate = CalcInterestRate,
            TermMonths = CalcTermMonths,
            InterestType = DebtInterestType.Amortizing,
            CompoundingFrequency = DebtCompoundingFrequency.Monthly,
            StartDate = DateTime.UtcNow.Date
        });

        if (!result.IsValid)
        {
            CalcSummary = string.Join("; ", result.Errors);
            CalcPaymentText = CalcTotalText = CalcInterestText = "—";
            return;
        }

        var payment = result.MonthlyPayment ?? 0m;
        var total = result.TotalOwed ?? 0m;
        var interest = result.InterestAmount ?? 0m;

        CalcPaymentText = _money.Format(_currency, payment);
        CalcTotalText = _money.Format(_currency, total);
        CalcInterestText = _money.Format(_currency, interest);
        CalcSummary = $"Amortizing over {CalcTermMonths} months at {CalcInterestRate:0.##}%.";
    }

    [RelayCommand]
    private void ApplyCalculationToForm()
    {
        if (CalcPrincipal <= 0 || CalcTermMonths <= 0)
        {
            Calculate();
            return;
        }

        var result = _calculator.Calculate(new DebtCalculationInput
        {
            Principal = CalcPrincipal,
            InterestRate = CalcInterestRate,
            TermMonths = CalcTermMonths,
            InterestType = DebtInterestType.Amortizing,
            CompoundingFrequency = DebtCompoundingFrequency.Monthly,
            StartDate = DateTime.UtcNow.Date
        });

        if (!result.IsValid)
        {
            CalcSummary = string.Join("; ", result.Errors);
            return;
        }

        var payment = result.MonthlyPayment ?? 0m;
        var total = result.TotalOwed ?? 0m;
        var interest = result.InterestAmount ?? 0m;

        CalcPaymentText = _money.Format(_currency, payment);
        CalcTotalText = _money.Format(_currency, total);
        CalcInterestText = _money.Format(_currency, interest);
        CalcSummary = $"Amortizing over {CalcTermMonths} months at {CalcInterestRate:0.##}%.";

        NewBalance = CalcPrincipal;
        NewTotal = CalcPrincipal;
        NewInterestRate = CalcInterestRate;
        NewMonthlyPayment = payment;
        StatusMessage = "Calculator values copied into the new debt form.";
    }

    [RelayCommand]
    private async Task AddDebtAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDebtName))
        {
            StatusMessage = "Enter a name for the debt account.";
            return;
        }

        if (NewBalance < 0)
        {
            StatusMessage = "Current balance cannot be negative.";
            return;
        }

        IsBusy = true;
        try
        {
            await _debts.CreateAsync(new DebtAccount
            {
                Name = NewDebtName.Trim(),
                Provider = string.IsNullOrWhiteSpace(NewProvider) ? null : NewProvider.Trim(),
                CurrentBalance = NewBalance,
                TotalAmount = NewTotal > 0 ? NewTotal : NewBalance,
                InterestRatePercent = NewInterestRate,
                MonthlyPaymentAmount = NewMonthlyPayment,
                PaymentStartDateUtc = DateTime.UtcNow.Date,
                PlannedTermMonths = CalcTermMonths > 0 ? CalcTermMonths : null
            });

            NewDebtName = "";
            NewProvider = "";
            NewBalance = 0;
            NewTotal = 0;
            NewInterestRate = 0;
            NewMonthlyPayment = 0;
            StatusMessage = "Debt account saved.";
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save debt: {ex.Message}";
            IsBusy = false;
        }
    }
}
