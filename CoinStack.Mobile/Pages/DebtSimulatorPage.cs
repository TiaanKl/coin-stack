using CoinStack.Mobile.Core;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Pages;

public sealed class DebtSimulatorPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;
    private readonly Picker _debtPicker;
    private readonly Slider _paymentSlider;
    private readonly Label _paymentLabel;
    private readonly Label _payoffDateLabel;
    private readonly Label _totalInterestLabel;
    private readonly Label _currentBalanceLabel;
    private readonly Label _interestRateLabel;
    private readonly Label _currentPaymentLabel;
    private IReadOnlyList<MobileDebtSummary> _debts = [];

    public DebtSimulatorPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Debt Simulator";
        BackgroundColor = AppColors.Background;

        _debtPicker = new Picker { Title = "Select a debt", FontFamily = "SpaceGroteskRegular" };
        _debtPicker.SelectedIndexChanged += OnDebtSelected;

        _paymentSlider = new Slider { Minimum = 0, Maximum = 5000, Value = 0, MinimumTrackColor = AppColors.Accent, ThumbColor = AppColors.Dark };
        _paymentSlider.ValueChanged += OnSliderChanged;

        _paymentLabel = new Label { Text = "$0.00", FontFamily = "SpaceGroteskBold", FontSize = 22, TextColor = AppColors.Dark, HorizontalTextAlignment = TextAlignment.Center };
        _payoffDateLabel = new Label { Text = "--", FontFamily = "SpaceGroteskBold", FontSize = 16, TextColor = AppColors.Success };
        _totalInterestLabel = new Label { Text = "--", FontFamily = "SpaceGroteskBold", FontSize = 16, TextColor = AppColors.Danger };
        _currentBalanceLabel = new Label { Text = "--", FontFamily = "SpaceGroteskRegular", FontSize = 14, TextColor = AppColors.Muted };
        _interestRateLabel = new Label { Text = "--", FontFamily = "SpaceGroteskRegular", FontSize = 14, TextColor = AppColors.Muted };
        _currentPaymentLabel = new Label { Text = "--", FontFamily = "SpaceGroteskRegular", FontSize = 14, TextColor = AppColors.Muted };

        var debtInfoCard = new Border
        {
            BackgroundColor = AppColors.Surface,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = new SolidColorBrush(AppColors.Border),
            StrokeThickness = 1,
            Padding = new Thickness(20),
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    CreateInfoRow("Current Balance", _currentBalanceLabel),
                    CreateInfoRow("Interest Rate", _interestRateLabel),
                    CreateInfoRow("Current Payment", _currentPaymentLabel),
                }
            }
        };

        var simulatorCard = new Border
        {
            BackgroundColor = AppColors.Surface,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = new SolidColorBrush(AppColors.Border),
            StrokeThickness = 1,
            Padding = new Thickness(20),
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Adjust Monthly Payment", FontFamily = "SpaceGroteskBold", FontSize = 16, TextColor = AppColors.Dark },
                    _paymentLabel,
                    _paymentSlider,
                    new BoxView { HeightRequest = 1, Color = AppColors.Border },
                    CreateInfoRow("Estimated Payoff", _payoffDateLabel),
                    CreateInfoRow("Est. Total Interest", _totalInterestLabel),
                }
            }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new Label { Text = "Debt Payoff Simulator", FontFamily = "SpaceGroteskBold", FontSize = 24, TextColor = AppColors.Dark },
                    new Label { Text = "Adjust payments to see how they affect your payoff timeline.", FontSize = 14, TextColor = AppColors.Muted, FontFamily = "SpaceGroteskRegular" },
                    _debtPicker,
                    debtInfoCard,
                    simulatorCard
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
            _debts = await _financeService.GetDebtsAsync();
            _debtPicker.Items.Clear();
            foreach (var d in _debts)
                _debtPicker.Items.Add($"{d.Name} ({MoneyDisplay.Format("USD", d.CurrentBalance)})");

            if (_debts.Count > 0)
                _debtPicker.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private void OnDebtSelected(object? sender, EventArgs e)
    {
        if (_debtPicker.SelectedIndex < 0 || _debtPicker.SelectedIndex >= _debts.Count) return;

        var debt = _debts[_debtPicker.SelectedIndex];
        _currentBalanceLabel.Text = MoneyDisplay.Format("USD", debt.CurrentBalance);
        _interestRateLabel.Text = $"{debt.InterestRatePercent:F2}%";
        _currentPaymentLabel.Text = MoneyDisplay.Format("USD", debt.MonthlyPaymentAmount);

        _paymentSlider.Maximum = Math.Max((double)debt.CurrentBalance / 2, (double)debt.MonthlyPaymentAmount * 5);
        _paymentSlider.Minimum = Math.Max(1, (double)debt.MonthlyPaymentAmount * 0.5);
        _paymentSlider.Value = (double)debt.MonthlyPaymentAmount;

        Recalculate();
    }

    private void OnSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        Recalculate();
    }

    private void Recalculate()
    {
        if (_debtPicker.SelectedIndex < 0 || _debtPicker.SelectedIndex >= _debts.Count) return;

        var debt = _debts[_debtPicker.SelectedIndex];
        var payment = (decimal)_paymentSlider.Value;
        _paymentLabel.Text = MoneyDisplay.Format("USD", payment);

        if (payment <= 0)
        {
            _payoffDateLabel.Text = "Never";
            _totalInterestLabel.Text = "--";
            return;
        }

        var balance = debt.CurrentBalance;
        var monthlyRate = debt.InterestRatePercent / 100m / 12m;
        var months = 0;
        var totalInterest = 0m;

        while (balance > 0 && months < 600)
        {
            var interest = balance * monthlyRate;
            totalInterest += interest;
            balance = balance + interest - payment;
            months++;

            if (payment <= interest)
            {
                _payoffDateLabel.Text = "Never (payment too low)";
                _totalInterestLabel.Text = "--";
                return;
            }
        }

        var payoffDate = DateTime.UtcNow.AddMonths(months);
        _payoffDateLabel.Text = $"{payoffDate:MMM yyyy} ({months} months)";
        _totalInterestLabel.Text = MoneyDisplay.Format("USD", totalInterest);
    }

    private static View CreateInfoRow(string label, Label valueLabel)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        var lbl = new Label { Text = label, FontSize = 14, TextColor = AppColors.Muted, FontFamily = "SpaceGroteskRegular", VerticalOptions = LayoutOptions.Center };
        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(valueLabel, 1);
        grid.Children.Add(lbl);
        grid.Children.Add(valueLabel);
        return grid;
    }
}
