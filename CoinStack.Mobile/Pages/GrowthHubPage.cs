using CoinStack.Mobile.Helpers;

namespace CoinStack.Mobile.Pages;

public sealed class GrowthHubPage : ContentPage
{
    public GrowthHubPage()
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
        Title = "Growth";

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
                        Text = "Personal Growth",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = AppColors.Dark,
                        Margin = new Thickness(0, 0, 0, 8)
                    },
                    NavigationCardBuilder.Create("Daily Challenges", "Earn XP with healthy financial habits", AppIcons.GlyphBolt, "challenges"),
                    NavigationCardBuilder.Create("Achievements", "Unlock badges as you progress", AppIcons.GlyphStar, "achievements"),
                    NavigationCardBuilder.Create("Reflections", "Reflect on your spending patterns", AppIcons.GlyphSeedling, "reflections"),
                    NavigationCardBuilder.Create("Mindset Journal", "Challenge unhelpful financial thoughts", AppIcons.GlyphBookOpen, "cbt-journal"),
                    NavigationCardBuilder.Create("Waitlist", "Pause impulse purchases", AppIcons.GlyphCartShopping, "waitlist"),
                    NavigationCardBuilder.Create("Weekly Recap", "Your week at a glance", AppIcons.GlyphCalendarWeek, "weekly-recap"),
                }
            }
        };
    }
}
