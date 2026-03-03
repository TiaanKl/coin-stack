using CoinStack.Data.Entities;
using CoinStack.Mobile.Core;
using CoinStack.Mobile.Services;

namespace CoinStack.Mobile.Pages;

public sealed class SubscriptionsPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    private readonly Entry _nameEntry;
    private readonly Entry _categoryEntry;
    private readonly Entry _costEntry;
    private readonly Picker _cyclePicker;
    private readonly Picker _statusPicker;
    private readonly Entry _debitDayEntry;
    private readonly VerticalStackLayout _list;

    public SubscriptionsPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Subscriptions";

        _nameEntry = new Entry { Placeholder = "Name (e.g. Netflix)" };
        _categoryEntry = new Entry { Placeholder = "Category" };
        _costEntry = new Entry { Placeholder = "Cost", Keyboard = Keyboard.Numeric };
        _cyclePicker = new Picker { Title = "Cycle", ItemsSource = Enum.GetValues<SubscriptionCycle>().ToList() };
        _statusPicker = new Picker { Title = "Status", ItemsSource = Enum.GetValues<SubscriptionStatus>().ToList() };
        _debitDayEntry = new Entry { Placeholder = "Debit day (optional)", Keyboard = Keyboard.Numeric };
        _cyclePicker.SelectedItem = SubscriptionCycle.Monthly;
        _statusPicker.SelectedItem = SubscriptionStatus.Active;

        var addButton = new Button { Text = "Add Subscription" };
        addButton.Clicked += async (_, _) => await AddSubscriptionAsync();

        _list = new VerticalStackLayout { Spacing = 8 };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 10,
                Children =
                {
                    _nameEntry,
                    _categoryEntry,
                    _costEntry,
                    _cyclePicker,
                    _statusPicker,
                    _debitDayEntry,
                    addButton,
                    new BoxView { HeightRequest = 1 },
                    new Label { Text = "Current Subscriptions", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    _list
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task AddSubscriptionAsync()
    {
        if (!decimal.TryParse(_costEntry.Text, out var cost))
        {
            await DisplayAlertAsync("Validation", "Enter a valid cost.", "OK");
            return;
        }

        int? debitDay = null;
        if (!string.IsNullOrWhiteSpace(_debitDayEntry.Text))
        {
            if (int.TryParse(_debitDayEntry.Text, out var parsedDay) && parsedDay is >= 1 and <= 31)
            {
                debitDay = parsedDay;
            }
            else
            {
                await DisplayAlertAsync("Validation", "Debit day must be between 1 and 31.", "OK");
                return;
            }
        }

        var cycle = _cyclePicker.SelectedItem as SubscriptionCycle? ?? SubscriptionCycle.Monthly;
        var status = _statusPicker.SelectedItem as SubscriptionStatus? ?? SubscriptionStatus.Active;

        try
        {
            await _financeService.AddSubscriptionAsync(_nameEntry.Text ?? string.Empty, _categoryEntry.Text ?? string.Empty, cost, cycle, status, debitDay);
            _nameEntry.Text = string.Empty;
            _categoryEntry.Text = string.Empty;
            _costEntry.Text = string.Empty;
            _debitDayEntry.Text = string.Empty;
            _cyclePicker.SelectedItem = SubscriptionCycle.Monthly;
            _statusPicker.SelectedItem = SubscriptionStatus.Active;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async Task DeleteAsync(int id)
    {
        await _financeService.DeleteSubscriptionAsync(id);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var settings = await _financeService.GetSettingsAsync();
            var subscriptions = await _financeService.GetSubscriptionsAsync();

            _list.Children.Clear();
            if (subscriptions.Count == 0)
            {
                _list.Children.Add(new Label { Text = "No subscriptions yet." });
                return;
            }

            foreach (var sub in subscriptions)
            {
                var row = new HorizontalStackLayout { Spacing = 8 };
                row.Children.Add(new Label
                {
                    Text = $"{sub.Name} • {sub.Category} • {sub.Cycle} • {MoneyDisplay.Format(settings.Currency, sub.Cost)} • {sub.Status}",
                    FontSize = 13,
                    HorizontalOptions = LayoutOptions.Fill
                });

                var deleteButton = new Button
                {
                    Text = "Delete",
                    FontSize = 12,
                    Padding = new Thickness(10, 4)
                };
                var id = sub.Id;
                deleteButton.Clicked += async (_, _) => await DeleteAsync(id);
                row.Children.Add(deleteButton);

                _list.Children.Add(row);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
