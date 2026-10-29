using CoinStack.Data.Entities;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using CoinStack.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Pages;

public sealed class SettingsPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    private readonly Picker _currencyPicker;
    private readonly Entry _incomeEntry;
    private readonly Picker _monthStartPicker;
    private readonly Picker _themePicker;
    private readonly Switch _scoringSwitch;
    private readonly Switch _streakSwitch;
    private readonly Switch _reflectionSwitch;

    private static readonly List<string> SupportedCurrencies = ["USD", "EUR", "GBP", "CAD", "AUD", "ZAR"];
    private static readonly List<int> MonthStartDays = Enumerable.Range(1, BudgetPeriod.LastPayday).ToList();
    private static readonly List<string> ThemeOptions = ["System", "Light", "Dark"];

    private AppSettings _settings = new();

    public SettingsPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Settings";

        _currencyPicker = new Picker { Title = "Select currency", ItemsSource = SupportedCurrencies };
        _incomeEntry = new Entry { Placeholder = "Monthly income", Keyboard = Keyboard.Numeric };
        _monthStartPicker = new Picker { Title = "Select payday", ItemsSource = MonthStartDays };
        _themePicker = new Picker { Title = "Select theme", ItemsSource = ThemeOptions };
        _themePicker.SelectedIndex = (int)AppThemeManager.GetSavedTheme();
        _themePicker.SelectedIndexChanged += OnThemeChanged;
        _scoringSwitch = new Switch();
        _streakSwitch = new Switch();
        _reflectionSwitch = new Switch();

        BuildContent();
    }

    private void BuildContent()
    {
        var saveButton = new Button { Text = "Save Settings" };
        saveButton.Clicked += async (_, _) => await SaveAsync();

        // ── General Card ──
        var generalCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label { Text = "General", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                new Label { Text = "Currency", FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Muted },
                _currencyPicker,
                new Label { Text = "Monthly Income", FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Muted },
                _incomeEntry,
                new Label { Text = "Payday", FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Muted },
                _monthStartPicker
            }
        });

        // ── Features Card ──
        var featuresCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label { Text = "Features", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                BuildToggleRow("Enable scoring", _scoringSwitch),
                BuildToggleRow("Enable streaks", _streakSwitch),
                BuildToggleRow("Enable reflections", _reflectionSwitch)
            }
        });

        // ── Appearance Card ──
        var appearanceCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label { Text = "Appearance", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                new Label { Text = "Theme", FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Muted },
                _themePicker
            }
        });

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children = { generalCard, featuresCard, appearanceCard, saveButton, BuildBackupCard() }
            }
        };
    }

    // ── Backup & restore: the data is only on this phone, so this keeps it safe and moves it to a new one ──

    private readonly Entry _backupPassword = new() { Placeholder = "Password (at least 8 characters)", IsPassword = true };
    private readonly Entry _backupConfirm = new() { Placeholder = "The same password again", IsPassword = true };
    private readonly Entry _restorePassword = new() { Placeholder = "The backup's password", IsPassword = true };
    private readonly Label _restoreFileLabel = new() { FontSize = 13, TextColor = AppColors.Muted, IsVisible = false };
    private readonly Label _restoreSummaryLabel = new() { FontSize = 13, TextColor = AppColors.Dark, IsVisible = false };
    private readonly Button _openBackupButton = new() { Text = "Open backup", IsVisible = false };
    private readonly Button _restoreButton = new() { Text = "Replace everything with this backup", IsVisible = false };
    private byte[]? _restoreFile;

    private View BuildBackupCard()
    {
        var backupButton = new Button { Text = "Back up" };
        backupButton.Clicked += async (_, _) => await BackUpAsync();

        var chooseButton = new Button { Text = "Choose a backup file" };
        chooseButton.Clicked += async (_, _) => await ChooseBackupAsync();
        _openBackupButton.Clicked += async (_, _) => await OpenBackupAsync();
        _restoreButton.Clicked += async (_, _) => await RestoreAsync();
        _restorePassword.IsVisible = false;

        return CreateCard(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label { Text = "Backup & Restore", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                new Label
                {
                    Text = "Your data is only on this phone. A backup is one file, locked with a password you choose. Save it to iCloud Drive, Google Drive or anywhere safe, and restore it here or in the CoinStack desktop or web app.",
                    FontSize = 13, TextColor = AppColors.Muted
                },
                _backupPassword,
                _backupConfirm,
                new Label { Text = "If you forget this password, the backup can't be opened. Nobody can recover it.", FontSize = 12, TextColor = Color.FromArgb("#B54708") },
                backupButton,
                new BoxView { HeightRequest = 1, Color = Color.FromArgb("#E4E7EC") },
                new Label { Text = "Restore", FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Muted },
                new Label { Text = "Replaces everything on this phone with the backup. A copy of what's here now is kept.", FontSize = 13, TextColor = AppColors.Muted },
                chooseButton,
                _restoreFileLabel,
                _restorePassword,
                _openBackupButton,
                _restoreSummaryLabel,
                _restoreButton
            }
        });
    }

    private async Task BackUpAsync()
    {
        var password = _backupPassword.Text ?? "";
        if (password != (_backupConfirm.Text ?? ""))
        {
            await DisplayAlertAsync("Backup", "The two passwords don't match.", "OK");
            return;
        }

        try
        {
            var backup = await _financeService.CreateBackupAsync(password);
            var path = System.IO.Path.Combine(FileSystem.CacheDirectory, _financeService.BackupFileName);
            await File.WriteAllBytesAsync(path, backup);

            // The share sheet offers Files, iCloud Drive, Google Drive, email and the rest.
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Save your CoinStack backup",
                File = new ShareFile(path, "application/octet-stream")
            });

            _backupPassword.Text = _backupConfirm.Text = "";
        }
        catch (BackupException ex)
        {
            await DisplayAlertAsync("Backup", ex.Message, "OK");
        }
    }

    private async Task ChooseBackupAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose a CoinStack backup" });
        if (file is null)
        {
            return;
        }

        await using var stream = await file.OpenReadAsync();
        if (stream.CanSeek && stream.Length > BackupService.MaxBackupBytes)
        {
            await DisplayAlertAsync("Restore", "That file is too large to be a CoinStack backup.", "OK");
            return;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        _restoreFile = buffer.ToArray();

        _restoreFileLabel.Text = file.FileName;
        _restoreFileLabel.IsVisible = true;
        _restorePassword.Text = "";
        _restorePassword.IsVisible = true;
        _openBackupButton.IsVisible = true;
        _restoreSummaryLabel.IsVisible = false;
        _restoreButton.IsVisible = false;
    }

    private async Task OpenBackupAsync()
    {
        if (_restoreFile is null)
        {
            return;
        }

        try
        {
            var manifest = await _financeService.ReadBackupAsync(_restoreFile, _restorePassword.Text ?? "");
            _restoreSummaryLabel.Text = $"Made {manifest.CreatedUtc.ToLocalTime():d MMM yyyy, HH:mm} · {manifest.Transactions} transactions"
                                        + (manifest.FirstTransactionUtc is { } first && manifest.LastTransactionUtc is { } last
                                            ? $" from {first.ToLocalTime():d MMM yyyy} to {last.ToLocalTime():d MMM yyyy}"
                                            : "");
            _restoreSummaryLabel.IsVisible = true;
            _restoreButton.IsVisible = true;
        }
        catch (BackupException ex)
        {
            await DisplayAlertAsync("Restore", ex.Message, "OK");
        }
    }

    private async Task RestoreAsync()
    {
        if (_restoreFile is null
            || !await DisplayAlertAsync("Restore backup?", "Everything on this phone is replaced with the backup. A copy of the current data is kept.", "Restore", "Cancel"))
        {
            return;
        }

        try
        {
            await _financeService.RestoreBackupAsync(_restoreFile, _restorePassword.Text ?? "");
            _restoreFile = null;
            _restoreFileLabel.IsVisible = _restorePassword.IsVisible = _openBackupButton.IsVisible = false;
            _restoreSummaryLabel.IsVisible = _restoreButton.IsVisible = false;
            await DisplayAlertAsync("Restored", "Your backup is restored. Every page shows the restored data when you open it.", "OK");
            await LoadAsync();
        }
        catch (BackupException ex)
        {
            await DisplayAlertAsync("Restore", ex.Message, "OK");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _settings = await _financeService.GetSettingsAsync();

            var currencyIndex = SupportedCurrencies.IndexOf(_settings.Currency);
            _currencyPicker.SelectedIndex = currencyIndex >= 0 ? currencyIndex : 0;

            _incomeEntry.Text = _settings.MonthlyIncome.ToString("0.##");

            var dayIndex = MonthStartDays.IndexOf(_settings.MonthStartDay);
            _monthStartPicker.SelectedIndex = dayIndex >= 0 ? dayIndex : 0;

            _scoringSwitch.IsToggled = _settings.EnableScoring;
            _streakSwitch.IsToggled = _settings.EnableStreaks;
            _reflectionSwitch.IsToggled = _settings.EnableReflections;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async Task SaveAsync()
    {
        if (!decimal.TryParse(_incomeEntry.Text, out var monthlyIncome))
        {
            await DisplayAlertAsync("Validation", "Enter a valid monthly income.", "OK");
            return;
        }

        var currency = _currencyPicker.SelectedItem as string ?? AppSettings.DefaultCurrency;
        var monthStart = _monthStartPicker.SelectedItem is int day ? day : 1;

        _settings.Currency = currency;
        _settings.MonthlyIncome = monthlyIncome;
        _settings.MonthStartDay = monthStart;
        _settings.EnableScoring = _scoringSwitch.IsToggled;
        _settings.EnableStreaks = _streakSwitch.IsToggled;
        _settings.EnableReflections = _reflectionSwitch.IsToggled;

        try
        {
            await _financeService.SaveSettingsAsync(_settings);
            await DisplayAlertAsync("Saved", "Settings updated.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private static View BuildToggleRow(string label, Switch toggle)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
        };
        grid.Add(new Label { Text = label, FontFamily = "InterRegular", FontSize = 14, TextColor = AppColors.Dark, VerticalOptions = LayoutOptions.Center }, 0, 0);
        grid.Add(toggle, 1, 0);
        return grid;
    }

    private static Border CreateCard(View content) => new()
    {
        BackgroundColor = AppColors.Surface,
        StrokeShape = new RoundRectangle { CornerRadius = 16 },
        Stroke = new SolidColorBrush(AppColors.Border),
        StrokeThickness = 1,
        Padding = new Thickness(16),
        Content = content
    };

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_themePicker.SelectedIndex < 0) return;
        var theme = (AppThemeManager.ThemeOption)_themePicker.SelectedIndex;
        AppThemeManager.SetTheme(theme);

        // Update the shell chrome (tab bar, nav bar) without recreating the shell
        if (Shell.Current is AppShell shell)
            shell.ApplyThemeColors();

        // Rebuild this page's content so cards pick up the new colour tokens
        BuildContent();
    }
}
