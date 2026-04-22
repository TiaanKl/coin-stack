using CoinStack.Data.Entities;
using CoinStack.Mobile.Core;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Pages;

public sealed class SubscriptionsPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    private readonly Entry _nameEntry;
    private readonly Picker _categoryPicker;
    private readonly Entry _costEntry;
    private readonly Picker _cyclePicker;
    private readonly Picker _statusPicker;
    private readonly Picker _debitDayPicker;
    private readonly VerticalStackLayout _list;

    private static readonly List<string> DebitDayOptions = new(["None (optional)"]) { Capacity = 32 };

    static SubscriptionsPage()
    {
        for (var i = 1; i <= 31; i++) DebitDayOptions.Add(i.ToString());
    }

    public SubscriptionsPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Subscriptions";

        _nameEntry = new Entry { Placeholder = "Name (e.g. Netflix)" };
        _categoryPicker = new Picker { Title = "Select category" };
        _costEntry = new Entry { Placeholder = "Cost", Keyboard = Keyboard.Numeric };
        _cyclePicker = new Picker { Title = "Cycle", ItemsSource = Enum.GetValues<SubscriptionCycle>().ToList() };
        _statusPicker = new Picker { Title = "Status", ItemsSource = Enum.GetValues<SubscriptionStatus>().ToList() };
        _debitDayPicker = new Picker { Title = "Debit day (optional)", ItemsSource = DebitDayOptions };
        _debitDayPicker.SelectedIndex = 0;
        _cyclePicker.SelectedItem = SubscriptionCycle.Monthly;
        _statusPicker.SelectedItem = SubscriptionStatus.Active;

        var addButton = new Button { Text = "Add Subscription" };
        addButton.Clicked += async (_, _) => await AddSubscriptionAsync();

        _list = new VerticalStackLayout { Spacing = 10 };

        // ── Add Subscription Card ──
        var formCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label { Text = "New Subscription", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                _nameEntry,
                _categoryPicker,
                _costEntry,
                _cyclePicker,
                _statusPicker,
                _debitDayPicker,
                addButton
            }
        });

        // ── Subscriptions List Card ──
        var listCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Current Subscriptions", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                _list
            }
        });

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children = { formCard, listCard }
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
        if (_debitDayPicker.SelectedIndex > 0 && int.TryParse(_debitDayPicker.SelectedItem as string, out var parsedDay))
        {
            debitDay = parsedDay;
        }

        var category = _categoryPicker.SelectedItem as string ?? string.Empty;
        var cycle = _cyclePicker.SelectedItem as SubscriptionCycle? ?? SubscriptionCycle.Monthly;
        var status = _statusPicker.SelectedItem as SubscriptionStatus? ?? SubscriptionStatus.Active;

        try
        {
            await _financeService.AddSubscriptionAsync(_nameEntry.Text ?? string.Empty, category, cost, cycle, status, debitDay);
            _nameEntry.Text = string.Empty;
            _categoryPicker.SelectedIndex = -1;
            _costEntry.Text = string.Empty;
            _debitDayPicker.SelectedIndex = 0;
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

            // Populate category picker from existing categories
            var categories = await _financeService.GetCategoriesAsync();
            var categoryNames = categories.Select(c => c.Name).OrderBy(n => n).ToList();
            _categoryPicker.ItemsSource = categoryNames;

            _list.Children.Clear();
            if (subscriptions.Count == 0)
            {
                _list.Children.Add(new Label { Text = "No subscriptions yet.", FontFamily = "InterRegular", TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 16) });
                return;
            }

            foreach (var sub in subscriptions)
            {
                var statusColor = sub.Status == SubscriptionStatus.Active ? AppColors.Success : AppColors.Muted;

                var row = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
                    RowSpacing = 4,
                    Padding = new Thickness(12, 10)
                };

                row.Add(new Label { Text = sub.Name, FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark }, 0, 0);
                row.Add(new Label { Text = MoneyDisplay.Format(settings.Currency, sub.Cost), FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark, HorizontalOptions = LayoutOptions.End }, 1, 0);
                row.Add(new Label { Text = $"{sub.Category} · {sub.Cycle}", FontSize = 11, FontFamily = "InterRegular", TextColor = AppColors.Muted }, 0, 1);

                var statusBadge = new Border
                {
                    BackgroundColor = sub.Status == SubscriptionStatus.Active ? AppColors.BgSuccess : AppColors.SurfaceContainer,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Stroke = Brush.Transparent,
                    Padding = new Thickness(8, 4),
                    Content = new Label { Text = sub.Status.ToString(), FontSize = 11, FontFamily = "InterBold", TextColor = statusColor },
                    HorizontalOptions = LayoutOptions.End
                };
                row.Add(statusBadge, 1, 1);

                var deleteButton = new Button
                {
                    Text = "Delete",
                    FontSize = 12,
                    Padding = new Thickness(12, 6),
                    HeightRequest = 36,
                    CornerRadius = 18,
                    BackgroundColor = AppColors.Danger
                };
                var id = sub.Id;
                deleteButton.Clicked += async (_, _) => await DeleteAsync(id);
                row.Add(deleteButton, 1, 2);

                _list.Children.Add(new Border
                {
                    BackgroundColor = AppColors.SurfaceDim,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Stroke = new SolidColorBrush(AppColors.Border),
                    StrokeThickness = 1,
                    Content = row
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
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
}
