using CoinStack.Data.Entities;
using CoinStack.Mobile.Core;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

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

        // ── Add Transaction Card ──
        var formCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label { Text = "New Transaction", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                _amountEntry,
                _descriptionEntry,
                _typePicker,
                addButton
            }
        });

        // ── History Card ──
        var historyCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "History", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                _list
            }
        });

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children = { formCard, historyCard }
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
                _list.Children.Add(new Label { Text = "No transactions yet.", FontFamily = "InterRegular", TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 16) });
                return;
            }

            foreach (var tx in transactions.Take(50))
            {
                var amount = MoneyDisplay.Format(settings.Currency, tx.Amount);
                bool isIncome = tx.Type == TransactionType.Income;
                var prefix = isIncome ? "+" : "-";

                var row = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    Padding = new Thickness(12, 10),
                };

                var info = new VerticalStackLayout
                {
                    Children =
                    {
                        new Label { Text = tx.Description, FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark, LineBreakMode = LineBreakMode.TailTruncation },
                        new Label { Text = $"{tx.OccurredAtUtc:dd MMM yyyy} · {tx.Type}", FontSize = 11, TextColor = AppColors.Muted, FontFamily = "InterRegular" }
                    }
                };

                var amountLabel = new Label
                {
                    Text = $"{prefix}{amount}",
                    FontFamily = "InterBold",
                    FontSize = 14,
                    TextColor = isIncome ? AppColors.Success : AppColors.Dark,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.End
                };

                row.Add(info, 0, 0);
                row.Add(amountLabel, 1, 0);

                _list.Children.Add(new Border
                {
                    BackgroundColor = AppColors.SurfaceDim,
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
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
