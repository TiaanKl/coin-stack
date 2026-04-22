using CoinStack.Mobile.Helpers;

namespace CoinStack.Mobile.Pages;

public sealed class GoalsHubPage : ContentPage
{
    public GoalsHubPage()
    {
        BuildContent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BuildContent();
    }

    private void BuildContent()
    {
        Title = "Goals";

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 10,
                Children =
                {
                    new Label
                    {
                        Text = "Savings & Goals",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = AppColors.Dark,
                        Margin = new Thickness(0, 0, 0, 8)
                    },
                    NavigationCardBuilder.Create("Goals", "Set and track your savings targets", AppIcons.GlyphBullseye, "goals"),
                    NavigationCardBuilder.Create("Savings", "Active savings and emergency funds", AppIcons.GlyphPiggyBank, "savings"),
                    NavigationCardBuilder.Create("Debt", "Manage and pay down your debts", AppIcons.GlyphCreditCard, "debt"),
                    NavigationCardBuilder.Create("Debt Simulator", "See how payments affect payoff", AppIcons.GlyphCalculator, "debt-simulator"),
                    NavigationCardBuilder.Create("Fallback History", "Savings fallback coverage log", AppIcons.GlyphClockRotateLeft, "fallback-history"),
                }
            }
        };
    }
}
