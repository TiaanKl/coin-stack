using CoinStack.Mobile.Helpers;

namespace CoinStack.Mobile.Pages;

public sealed class MoreHubPage : ContentPage
{
    public MoreHubPage()
    {
        Title = "More";
        BackgroundColor = AppColors.Background;

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
                        Text = "Reports & Settings",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = AppColors.Dark,
                        Margin = new Thickness(0, 0, 0, 8)
                    },
                    NavigationCardBuilder.Create("Reports", "Monthly financial snapshot", AppIcons.GlyphChartBar, "reports"),
                    NavigationCardBuilder.Create("Settings", "Currency, income, and preferences", AppIcons.GlyphGear, "settings"),
                }
            }
        };
    }
}
