using CoinStack.Data.Entities;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
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
    private static readonly List<int> MonthStartDays = Enumerable.Range(1, 28).ToList();
    private static readonly List<string> ThemeOptions = ["System", "Light", "Dark"];

    private AppSettings _settings = new();

    public SettingsPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Settings";

        _currencyPicker = new Picker { Title = "Select currency", ItemsSource = SupportedCurrencies };
        _incomeEntry = new Entry { Placeholder = "Monthly income", Keyboard = Keyboard.Numeric };
        _monthStartPicker = new Picker { Title = "Select start day", ItemsSource = MonthStartDays };
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
                new Label { Text = "Month Start Day", FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Muted },
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
                Children = { generalCard, featuresCard, appearanceCard, saveButton }
            }
        };
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

        var currency = _currencyPicker.SelectedItem as string ?? "USD";
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
