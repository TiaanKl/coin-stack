using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoinStack.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ISettingsService _settings;

    [ObservableProperty] private string _currency = AppSettings.DefaultCurrency;
    [ObservableProperty] private int _monthStartDay = 1;
    [ObservableProperty] private decimal _monthlyIncome;
    [ObservableProperty] private decimal _currentBankBalance;
    [ObservableProperty] private bool _showReserveAwareBudget;
    [ObservableProperty] private bool _enableEmergencyFallback;
    [ObservableProperty] private bool _enableScoring = true;
    [ObservableProperty] private bool _enableStreaks = true;
    [ObservableProperty] private bool _enableToast = true;
    [ObservableProperty] private bool _enableSounds = true;
    [ObservableProperty] private bool _enableReflections = true;
    [ObservableProperty] private int _largeExpenseThreshold = 50;

    public ObservableCollection<string> Currencies { get; } =
        ["USD", "EUR", "GBP", "CAD", "AUD", "ZAR"];

    public ObservableCollection<int> StartDays { get; } =
        [.. Enumerable.Range(1, BudgetPeriod.LastPayday)];

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var settings = await _settings.GetAsync();
            Currency = settings.Currency;
            MonthStartDay = BudgetPeriod.NormalizePayday(settings.MonthStartDay);
            MonthlyIncome = settings.MonthlyIncome;
            CurrentBankBalance = settings.CurrentBankBalance;
            ShowReserveAwareBudget = settings.ShowReserveAwareBudget;
            EnableEmergencyFallback = settings.EnableEmergencyFallback;
            EnableScoring = settings.EnableScoring;
            EnableStreaks = settings.EnableStreaks;
            EnableToast = settings.EnableToast;
            EnableSounds = settings.EnableSounds;
            EnableReflections = settings.EnableReflections;
            LargeExpenseThreshold = settings.LargeExpenseThreshold;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load settings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _settings.GetAsync();
            settings.Currency = Currency;
            settings.MonthStartDay = MonthStartDay;
            settings.MonthlyIncome = MonthlyIncome;
            settings.CurrentBankBalance = CurrentBankBalance;
            settings.BankBalanceAsOfUtc = DateTime.UtcNow;
            settings.ShowReserveAwareBudget = ShowReserveAwareBudget;
            settings.EnableEmergencyFallback = EnableEmergencyFallback;
            settings.EnableScoring = EnableScoring;
            settings.EnableStreaks = EnableStreaks;
            settings.EnableToast = EnableToast;
            settings.EnableSounds = EnableSounds;
            settings.EnableReflections = EnableReflections;
            settings.LargeExpenseThreshold = LargeExpenseThreshold;

            await _settings.SaveAsync(settings);
            StatusMessage = "Settings saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save settings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
