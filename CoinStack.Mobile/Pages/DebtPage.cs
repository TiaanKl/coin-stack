using CoinStack.Mobile.Core;
using CoinStack.Mobile.Services;

namespace CoinStack.Mobile.Pages;

public sealed class DebtPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    private readonly Entry _nameEntry;
    private readonly Entry _providerEntry;
    private readonly Entry _totalEntry;
    private readonly Entry _balanceEntry;
    private readonly Entry _monthlyPaymentEntry;
    private readonly Entry _interestEntry;
    private readonly Entry _plannedTermEntry;
    private readonly VerticalStackLayout _list;

    public DebtPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Debt";

        _nameEntry = new Entry { Placeholder = "Debt name" };
        _providerEntry = new Entry { Placeholder = "Provider" };
        _totalEntry = new Entry { Placeholder = "Total amount", Keyboard = Keyboard.Numeric };
        _balanceEntry = new Entry { Placeholder = "Current balance", Keyboard = Keyboard.Numeric };
        _monthlyPaymentEntry = new Entry { Placeholder = "Monthly payment", Keyboard = Keyboard.Numeric };
        _interestEntry = new Entry { Placeholder = "Interest %", Keyboard = Keyboard.Numeric };
        _plannedTermEntry = new Entry { Placeholder = "Planned term months (optional)", Keyboard = Keyboard.Numeric };

        var addButton = new Button { Text = "Add Debt" };
        addButton.Clicked += async (_, _) => await AddDebtAsync();

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
                    _providerEntry,
                    _totalEntry,
                    _balanceEntry,
                    _monthlyPaymentEntry,
                    _interestEntry,
                    _plannedTermEntry,
                    addButton,
                    new BoxView { HeightRequest = 1 },
                    new Label { Text = "Debt Accounts", FontSize = 18, FontAttributes = FontAttributes.Bold },
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

    private async Task AddDebtAsync()
    {
        if (!decimal.TryParse(_totalEntry.Text, out var total) ||
            !decimal.TryParse(_balanceEntry.Text, out var balance) ||
            !decimal.TryParse(_monthlyPaymentEntry.Text, out var monthlyPayment) ||
            !decimal.TryParse(_interestEntry.Text, out var interest))
        {
            await DisplayAlertAsync("Validation", "Enter valid debt numbers.", "OK");
            return;
        }

        int? plannedTerm = null;
        if (!string.IsNullOrWhiteSpace(_plannedTermEntry.Text))
        {
            if (int.TryParse(_plannedTermEntry.Text, out var parsedTerm) && parsedTerm > 0)
            {
                plannedTerm = parsedTerm;
            }
            else
            {
                await DisplayAlertAsync("Validation", "Planned term must be a positive number.", "OK");
                return;
            }
        }

        try
        {
            await _financeService.AddDebtAsync(_nameEntry.Text ?? string.Empty, _providerEntry.Text, total, balance, monthlyPayment, interest, plannedTermMonths: plannedTerm);
            _nameEntry.Text = string.Empty;
            _providerEntry.Text = string.Empty;
            _totalEntry.Text = string.Empty;
            _balanceEntry.Text = string.Empty;
            _monthlyPaymentEntry.Text = string.Empty;
            _interestEntry.Text = string.Empty;
            _plannedTermEntry.Text = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async Task RecordPaymentAsync(int id)
    {
        var amount = await DisplayPromptAsync("Record Payment", "Payment amount:", "Save", "Cancel", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(amount) || !decimal.TryParse(amount, out var payment))
        {
            return;
        }

        await _financeService.RecordDebtPaymentAsync(id, payment);
        await LoadAsync();
    }

    private async Task DeleteAsync(int id)
    {
        await _financeService.DeleteDebtAsync(id);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var settings = await _financeService.GetSettingsAsync();
            var debts = await _financeService.GetDebtsAsync();

            _list.Children.Clear();
            if (debts.Count == 0)
            {
                _list.Children.Add(new Label { Text = "No debt accounts yet." });
                return;
            }

            foreach (var debt in debts)
            {
                var row = new VerticalStackLayout { Spacing = 4 };
                row.Children.Add(new Label
                {
                    Text = $"{debt.Name} ({debt.Provider ?? "No provider"})",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold
                });
                row.Children.Add(new Label
                {
                    Text = $"Balance: {MoneyDisplay.Format(settings.Currency, debt.CurrentBalance)} / {MoneyDisplay.Format(settings.Currency, debt.TotalAmount)}",
                    FontSize = 13
                });
                row.Children.Add(new Label
                {
                    Text = $"Monthly: {MoneyDisplay.Format(settings.Currency, debt.MonthlyPaymentAmount)} • Interest: {debt.InterestRatePercent:0.##}% • Est payoff: {(debt.EstimatedPayoffDateUtc.HasValue ? debt.EstimatedPayoffDateUtc.Value.ToString("dd MMM yyyy") : "N/A")}",
                    FontSize = 12
                });

                var actions = new HorizontalStackLayout { Spacing = 8 };
                var payButton = new Button { Text = "Record Payment", FontSize = 12, Padding = new Thickness(10, 4) };
                var deleteButton = new Button { Text = "Delete", FontSize = 12, Padding = new Thickness(10, 4) };
                var id = debt.Id;
                payButton.Clicked += async (_, _) => await RecordPaymentAsync(id);
                deleteButton.Clicked += async (_, _) => await DeleteAsync(id);
                actions.Children.Add(payButton);
                actions.Children.Add(deleteButton);

                row.Children.Add(actions);
                row.Children.Add(new BoxView { HeightRequest = 1 });
                _list.Children.Add(row);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
