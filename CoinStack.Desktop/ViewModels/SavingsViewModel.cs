using System;
using System.Linq;
using System.Threading.Tasks;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoinStack.Desktop.ViewModels;

public partial class SavingsViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ISavingsService _savings;
    private readonly ISettingsService _settings;
    private readonly IMoneyFormatter _money;

    private string _currency = AppSettings.DefaultCurrency;

    [ObservableProperty] private string _totalSavedText = "";
    [ObservableProperty] private string _availableText = "";
    [ObservableProperty] private string _reservedText = "";
    [ObservableProperty] private string _emergencyFundText = "";
    [ObservableProperty] private string _thisMonthText = "";
    [ObservableProperty] private string _interestEarnedText = "";
    [ObservableProperty] private bool _fallbackEnabled;
    [ObservableProperty] private string _lastCalculatedMonth = "—";
    [ObservableProperty] private decimal _transferAmount;

    public SavingsViewModel(
        ISavingsService savings,
        ISettingsService settings,
        IMoneyFormatter money)
    {
        _savings = savings;
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

            var state = await _savings.GetSnapshotAsync();
            TotalSavedText = _money.Format(_currency, state.SavingsBalance);
            AvailableText = _money.Format(_currency, state.Available);
            ReservedText = _money.Format(_currency, state.Reserved);
            EmergencyFundText = _money.Format(_currency, state.EmergencyBalance);
            FallbackEnabled = state.FallbackEnabled;
            LastCalculatedMonth = string.IsNullOrWhiteSpace(state.LastCalculatedMonth)
                ? "—"
                : state.LastCalculatedMonth;

            var summaries = await _savings.GetMonthlySummariesAsync();
            var latest = summaries.FirstOrDefault();
            ThisMonthText = _money.Format(_currency, latest?.Total ?? 0m);
            InterestEarnedText = _money.Format(_currency, summaries.Sum(s => s.Interest));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load savings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyMonthAsync()
    {
        IsBusy = true;
        try
        {
            var summary = await _savings.CalculateMonthAsync();
            StatusMessage = summary is null
                ? "Already calculated for this month."
                : $"Applied {_money.Format(_currency, summary.Total)} for {summary.Month}.";
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not apply month: {ex.Message}";
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddToSavingsAsync()
    {
        if (TransferAmount <= 0)
        {
            StatusMessage = "Enter a positive transfer amount.";
            return;
        }

        IsBusy = true;
        try
        {
            await _savings.AddToSavingsAsync(TransferAmount, "Desktop transfer");
            TransferAmount = 0;
            StatusMessage = "Added to savings.";
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Transfer failed: {ex.Message}";
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleFallbackAsync()
    {
        IsBusy = true;
        try
        {
            await _savings.SetFallbackEnabledAsync(!FallbackEnabled);
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not update fallback: {ex.Message}";
            IsBusy = false;
        }
    }
}
