using CoinStack.Mobile.Core;
using CoinStack.Mobile.Services;

namespace CoinStack.Mobile.Pages;

public sealed class BucketsPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    private readonly Entry _nameEntry;
    private readonly Entry _amountEntry;
    private readonly Switch _isSavingsSwitch;
    private readonly VerticalStackLayout _list;

    public BucketsPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Buckets";

        _nameEntry = new Entry { Placeholder = "Bucket name" };
        _amountEntry = new Entry { Placeholder = "Allocated amount", Keyboard = Keyboard.Numeric };
        _isSavingsSwitch = new Switch();

        var addButton = new Button { Text = "Add Bucket" };
        addButton.Clicked += async (_, _) => await AddBucketAsync();

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
                    _amountEntry,
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            new Label { Text = "Savings bucket", VerticalTextAlignment = TextAlignment.Center },
                            _isSavingsSwitch
                        }
                    },
                    addButton,
                    new BoxView { HeightRequest = 1 },
                    new Label { Text = "Current Buckets", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    _list
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadBucketsAsync();
    }

    private async Task AddBucketAsync()
    {
        if (!decimal.TryParse(_amountEntry.Text, out var amount))
        {
            await DisplayAlertAsync("Validation", "Enter a valid allocated amount.", "OK");
            return;
        }

        try
        {
            await _financeService.AddBucketAsync(_nameEntry.Text ?? string.Empty, amount, _isSavingsSwitch.IsToggled);
            _nameEntry.Text = string.Empty;
            _amountEntry.Text = string.Empty;
            _isSavingsSwitch.IsToggled = false;
            await LoadBucketsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async Task LoadBucketsAsync()
    {
        try
        {
            var settings = await _financeService.GetSettingsAsync();
            var buckets = await _financeService.GetBucketsAsync();

            _list.Children.Clear();
            if (buckets.Count == 0)
            {
                _list.Children.Add(new Label { Text = "No buckets yet." });
                return;
            }

            foreach (var bucket in buckets)
            {
                _list.Children.Add(new Label
                {
                    Text = $"{bucket.Name} • {MoneyDisplay.Format(settings.Currency, bucket.AllocatedAmount)} • {(bucket.IsSavings ? "Savings" : "Spending")}",
                    FontSize = 14
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
