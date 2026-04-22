using CoinStack.Mobile.Helpers;

namespace CoinStack.Mobile.Pages;

public sealed class MoneyHubPage : ContentPage
{
    public MoneyHubPage()
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
        Title = "Money";

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
                        Text = "Money Management",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = AppColors.Dark,
                        Margin = new Thickness(0, 0, 0, 8)
                    },
                    NavigationCardBuilder.Create("Transactions", "Track your spending and income", AppIcons.GlyphReceipt, "transactions"),
                    NavigationCardBuilder.Create("Income", "Manage your income streams", AppIcons.GlyphArrowTrendUp, "income"),
                    NavigationCardBuilder.Create("Budgets", "Spending envelopes and allocations", AppIcons.GlyphBuildingColumns, "buckets"),
                    NavigationCardBuilder.Create("Subscriptions", "Recurring payments and services", AppIcons.GlyphRotate, "subscriptions"),
                    NavigationCardBuilder.Create("Categories", "Organize your transactions", AppIcons.GlyphTags, "categories"),
                }
            }
        };
    }
}
