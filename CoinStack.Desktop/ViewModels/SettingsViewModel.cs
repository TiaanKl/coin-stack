using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CoinStack.Data.Entities;
using CoinStack.Desktop.Services;
using CoinStack.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoinStack.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ISettingsService _settings;
    private readonly IAccountService _accounts;

    // The main account's balance as loaded, so saving only resets its date when the figure was changed.
    private decimal _loadedBankBalance;

    [ObservableProperty] private string _currency = AppSettings.DefaultCurrency;
    [ObservableProperty] private int _monthStartDay = 1;
    [ObservableProperty] private decimal _monthlyIncome;
    [ObservableProperty] private decimal _currentBankBalance;
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

    // Backup & restore
    private readonly IBackupService _backups;
    private readonly IFileDialogs _files;
    private byte[]? _restoreFile;

    [ObservableProperty] private string _backupPassword = "";
    [ObservableProperty] private string _backupConfirm = "";
    [ObservableProperty] private string? _backupMessage;
    [ObservableProperty] private string? _restoreFileName;
    [ObservableProperty] private string _restorePassword = "";
    [ObservableProperty] private string? _restoreSummary;
    [ObservableProperty] private bool _confirmingRestore;
    [ObservableProperty] private string? _restoreMessage;

    public SettingsViewModel(ISettingsService settings, IAccountService accounts, IBackupService backups, IFileDialogs files)
    {
        _settings = settings;
        _accounts = accounts;
        _backups = backups;
        _files = files;
    }

    [RelayCommand]
    private async Task BackUpAsync()
    {
        if (BackupPassword != BackupConfirm)
        {
            BackupMessage = "The two passwords don't match.";
            return;
        }

        IsBusy = true;
        try
        {
            var backup = await _backups.CreateAsync(BackupPassword);
            if (await _files.SaveAsync(_backups.SuggestedFileName, backup))
            {
                BackupMessage = "Backup saved. Keep it somewhere other than this computer.";
                BackupPassword = BackupConfirm = "";
            }
        }
        catch (BackupException ex)
        {
            BackupMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ChooseBackupAsync()
    {
        var file = await _files.OpenAsync(BackupService.MaxBackupBytes);
        if (file is not { } chosen)
        {
            return;
        }

        _restoreFile = chosen.Content;
        RestoreFileName = chosen.Name;
        RestorePassword = "";
        RestoreSummary = null;
        ConfirmingRestore = false;
        RestoreMessage = null;
    }

    [RelayCommand]
    private async Task OpenBackupAsync()
    {
        if (_restoreFile is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var manifest = await _backups.ReadAsync(_restoreFile, RestorePassword);
            RestoreSummary = $"Made {manifest.CreatedUtc.ToLocalTime():d MMM yyyy, HH:mm} · {manifest.Transactions} transactions"
                             + (manifest.FirstTransactionUtc is { } first && manifest.LastTransactionUtc is { } last
                                 ? $" from {first.ToLocalTime():d MMM yyyy} to {last.ToLocalTime():d MMM yyyy}"
                                 : "");
            RestoreMessage = null;
        }
        catch (BackupException ex)
        {
            RestoreMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AskToRestore() => ConfirmingRestore = RestoreSummary is not null;

    [RelayCommand]
    private void CancelRestore() => ConfirmingRestore = false;

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (_restoreFile is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var safetyCopy = await _backups.RestoreAsync(_restoreFile, RestorePassword);
            RestoreMessage = "Restored. Every page shows the restored data when you open it."
                             + (safetyCopy is null ? "" : $" Your previous data was saved to {safetyCopy}.");
            _restoreFile = null;
            RestoreFileName = null;
            RestoreSummary = null;
            RestorePassword = "";
            await InitializeAsync();
        }
        catch (BackupException ex)
        {
            RestoreMessage = ex.Message;
        }
        finally
        {
            ConfirmingRestore = false;
            IsBusy = false;
        }
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
            var main = (await _accounts.GetBalancesAsync()).FirstOrDefault(a => a.Account.IsDefault);
            CurrentBankBalance = main?.Balance ?? 0m;
            _loadedBankBalance = CurrentBankBalance;
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
            if (CurrentBankBalance != _loadedBankBalance)
            {
                var main = await _accounts.GetDefaultAsync();
                await _accounts.SetBalanceAsync(main.Id, CurrentBankBalance, DateTime.UtcNow);
                _loadedBankBalance = CurrentBankBalance;
            }
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
