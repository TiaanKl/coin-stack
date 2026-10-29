using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CoinStack.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoinStack.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ISettingsService _settings;

    [ObservableProperty] private string _currency = "USD";
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
        [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28];

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
            MonthStartDay = settings.MonthStartDay is >= 1 and <= 28 ? settings.MonthStartDay : 1;
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
