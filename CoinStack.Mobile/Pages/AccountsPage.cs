using CoinStack.Mobile.Core;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using CoinStack.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Pages;

/// <summary>Where the money is: each account's balance, moving money between accounts, and updating a balance.</summary>
public sealed class AccountsPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    private readonly Label _spendableLabel;
    private readonly VerticalStackLayout _list;
    private readonly Picker _fromPicker;
    private readonly Picker _toPicker;
    private readonly Entry _transferAmountEntry;
    private readonly Picker _balancePicker;
    private readonly Entry _balanceEntry;

    private IReadOnlyList<AccountBalance> _accounts = [];

    public AccountsPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Accounts";

        _spendableLabel = new Label { FontFamily = "InterBold", FontSize = 24, TextColor = AppColors.Dark };
        _list = new VerticalStackLayout { Spacing = 8 };

        _fromPicker = new Picker { Title = "From" };
        _toPicker = new Picker { Title = "To" };
        _transferAmountEntry = new Entry { Placeholder = "Amount", Keyboard = Keyboard.Numeric };
        var transferButton = new Button { Text = "Transfer" };
        transferButton.Clicked += async (_, _) => await TransferAsync();

        _balancePicker = new Picker { Title = "Account" };
        _balanceEntry = new Entry { Placeholder = "What the bank shows today", Keyboard = Keyboard.Numeric };
        var balanceButton = new Button { Text = "Save balance" };
        balanceButton.Clicked += async (_, _) => await SaveBalanceAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    CreateCard(new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children =
                        {
                            new Label { Text = "Money to spend", FontFamily = "InterRegular", FontSize = 13, TextColor = AppColors.Muted },
                            _spendableLabel,
                            new Label { Text = "Your cheque accounts", FontFamily = "InterRegular", FontSize = 11, TextColor = AppColors.Muted }
                        }
                    }),
                    CreateCard(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            new Label { Text = "Accounts", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                            _list
                        }
                    }),
                    CreateCard(new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            new Label { Text = "Transfer", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                            new Label { Text = "Move money between your own accounts. It isn't spending or income.", FontSize = 12, TextColor = AppColors.Muted },
                            _fromPicker,
                            _toPicker,
                            _transferAmountEntry,
                            transferButton
                        }
                    }),
                    CreateCard(new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            new Label { Text = "Update balance", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                            new Label { Text = "Use a negative amount for an overdraft or money owed on a card.", FontSize = 12, TextColor = AppColors.Muted },
                            _balancePicker,
                            _balanceEntry,
                            balanceButton
                        }
                    })
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
            var settings = await _financeService.GetSettingsAsync();
            _accounts = await _financeService.GetAccountsAsync();
            _spendableLabel.Text = MoneyDisplay.Format(settings.Currency, await _financeService.GetSpendableBalanceAsync());

            var names = _accounts.Select(a => a.Account.Name).ToList();
            _fromPicker.ItemsSource = names;
            _toPicker.ItemsSource = names.ToList();
            _balancePicker.ItemsSource = names.ToList();
            _fromPicker.SelectedIndex = names.Count > 0 ? 0 : -1;
            _toPicker.SelectedIndex = names.Count > 1 ? 1 : -1;
            _balancePicker.SelectedIndex = names.Count > 0 ? 0 : -1;

            _list.Children.Clear();
            foreach (var entry in _accounts)
            {
                var row = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    Padding = new Thickness(12, 10)
                };

                var number = AccountDisplay.MaskedNumber(entry.Account.AccountNumber);
                row.Add(new VerticalStackLayout
                {
                    Children =
                    {
                        new Label { Text = entry.Account.Name, FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark },
                        new Label
                        {
                            Text = AccountDisplay.Label(entry.Account.Type) + (number is null ? "" : $" · {number}") + (entry.Account.IsDefault ? " · Main" : ""),
                            FontSize = 11, TextColor = AppColors.Muted, FontFamily = "InterRegular"
                        }
                    }
                }, 0, 0);
                row.Add(new Label
                {
                    Text = MoneyDisplay.Format(settings.Currency, entry.Balance),
                    FontFamily = "InterBold",
                    FontSize = 14,
                    TextColor = entry.Balance < 0 ? AppColors.Danger : AppColors.Dark,
                    VerticalOptions = LayoutOptions.Center
                }, 1, 0);

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

    private async Task TransferAsync()
    {
        if (!decimal.TryParse(_transferAmountEntry.Text, out var amount)
            || _fromPicker.SelectedIndex < 0 || _toPicker.SelectedIndex < 0)
        {
            await DisplayAlertAsync("Transfer", "Choose both accounts and enter an amount.", "OK");
            return;
        }

        try
        {
            await _financeService.TransferAsync(_accounts[_fromPicker.SelectedIndex].Account.Id, _accounts[_toPicker.SelectedIndex].Account.Id, amount);
            _transferAmountEntry.Text = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Transfer", ex.Message, "OK");
        }
    }

    private async Task SaveBalanceAsync()
    {
        if (!decimal.TryParse(_balanceEntry.Text, out var balance) || _balancePicker.SelectedIndex < 0)
        {
            await DisplayAlertAsync("Update balance", "Choose an account and enter its balance.", "OK");
            return;
        }

        try
        {
            await _financeService.SetAccountBalanceAsync(_accounts[_balancePicker.SelectedIndex].Account.Id, balance);
            _balanceEntry.Text = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Update balance", ex.Message, "OK");
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
