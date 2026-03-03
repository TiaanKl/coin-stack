using CoinStack.Data.Entities;
using CoinStack.Mobile.Core;
using CoinStack.Mobile.Services;

namespace CoinStack.Mobile.Pages;

public sealed class TransactionsPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    private readonly Entry _amountEntry;
    private readonly Entry _descriptionEntry;
    private readonly Picker _typePicker;
    private readonly VerticalStackLayout _list;

    public TransactionsPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Transactions";

        _amountEntry = new Entry { Placeholder = "Amount", Keyboard = Keyboard.Numeric };
        _descriptionEntry = new Entry { Placeholder = "Description" };
        _typePicker = new Picker { Title = "Type" };
        _typePicker.ItemsSource = Enum.GetValues<TransactionType>().ToList();
        _typePicker.SelectedItem = TransactionType.Expense;

        var addButton = new Button { Text = "Add Transaction" };
        addButton.Clicked += async (_, _) => await AddTransactionAsync();

        _list = new VerticalStackLayout { Spacing = 8 };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 10,
                Children =
                {
                    _amountEntry,
                    _descriptionEntry,
                    _typePicker,
                    addButton,
                    new BoxView { HeightRequest = 1 },
                    new Label { Text = "History", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    _list
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTransactionsAsync();
    }

    private async Task AddTransactionAsync()
    {
        if (!decimal.TryParse(_amountEntry.Text, out var amount))
        {
            await DisplayAlertAsync("Validation", "Enter a valid amount.", "OK");
            return;
        }

        var description = _descriptionEntry.Text ?? string.Empty;
        var selectedType = _typePicker.SelectedItem as TransactionType? ?? TransactionType.Expense;

        try
        {
            await _financeService.AddTransactionAsync(amount, selectedType, description);
            _amountEntry.Text = string.Empty;
            _descriptionEntry.Text = string.Empty;
            _typePicker.SelectedItem = TransactionType.Expense;
            await LoadTransactionsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async Task LoadTransactionsAsync()
    {
        try
        {
            var settings = await _financeService.GetSettingsAsync();
            var transactions = await _financeService.GetTransactionsAsync();

            _list.Children.Clear();
            if (transactions.Count == 0)
            {
                _list.Children.Add(new Label { Text = "No transactions yet." });
                return;
            }

            foreach (var tx in transactions.Take(50))
            {
                var amount = MoneyDisplay.Format(settings.Currency, tx.Amount);
                var prefix = tx.Type == TransactionType.Income ? "+" : "-";

                _list.Children.Add(new Label
                {
                    Text = $"{tx.OccurredAtUtc:dd MMM yyyy} • {tx.Description} • {prefix}{amount} ({tx.Type})",
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
