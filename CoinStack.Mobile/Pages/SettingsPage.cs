using CoinStack.Data.Entities;
using CoinStack.Mobile.Services;

namespace CoinStack.Mobile.Pages;

public sealed class SettingsPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    private readonly Entry _currencyEntry;
    private readonly Entry _incomeEntry;
    private readonly Entry _monthStartEntry;
    private readonly Switch _scoringSwitch;
    private readonly Switch _streakSwitch;
    private readonly Switch _reflectionSwitch;

    private AppSettings _settings = new();

    public SettingsPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Settings";

        _currencyEntry = new Entry { Placeholder = "Currency code (e.g. USD)" };
        _incomeEntry = new Entry { Placeholder = "Monthly income", Keyboard = Keyboard.Numeric };
        _monthStartEntry = new Entry { Placeholder = "Month start day (1-28)", Keyboard = Keyboard.Numeric };
        _scoringSwitch = new Switch();
        _streakSwitch = new Switch();
        _reflectionSwitch = new Switch();

        var saveButton = new Button { Text = "Save Settings" };
        saveButton.Clicked += async (_, _) => await SaveAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 10,
                Children =
                {
                    _currencyEntry,
                    _incomeEntry,
                    _monthStartEntry,
                    BuildToggleRow("Enable scoring", _scoringSwitch),
                    BuildToggleRow("Enable streaks", _streakSwitch),
                    BuildToggleRow("Enable reflections", _reflectionSwitch),
                    saveButton
                }
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
            _currencyEntry.Text = _settings.Currency;
            _incomeEntry.Text = _settings.MonthlyIncome.ToString("0.##");
            _monthStartEntry.Text = _settings.MonthStartDay.ToString();
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

        if (!int.TryParse(_monthStartEntry.Text, out var monthStart) || monthStart < 1 || monthStart > 28)
        {
            await DisplayAlertAsync("Validation", "Month start day must be between 1 and 28.", "OK");
            return;
        }

        _settings.Currency = (_currencyEntry.Text ?? "USD").Trim().ToUpperInvariant();
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

    private static HorizontalStackLayout BuildToggleRow(string label, Switch toggle)
    {
        return new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = label, VerticalTextAlignment = TextAlignment.Center },
                toggle
            }
        };
    }
}
