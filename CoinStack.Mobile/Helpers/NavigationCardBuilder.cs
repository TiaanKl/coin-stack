using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Helpers;

/// <summary>
/// Builds consistent navigation card items for hub pages.
/// </summary>
public static class NavigationCardBuilder
{
    public static View Create(string title, string subtitle, string iconGlyph, string route)
    {
        var iconLabel = AppIcons.CreateLabel(iconGlyph, AppColors.Dark, 18);
        iconLabel.VerticalOptions = LayoutOptions.Center;
        iconLabel.HorizontalOptions = LayoutOptions.Center;

        var iconFrame = new Border
        {
            BackgroundColor = AppColors.NavIconBg,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Stroke = Colors.Transparent,
            StrokeThickness = 0,
            Padding = new Thickness(10),
            Content = iconLabel,
            WidthRequest = 44,
            HeightRequest = 44,
            VerticalOptions = LayoutOptions.Center
        };

        var chevron = AppIcons.CreateLabel(AppIcons.GlyphChevronRight, AppColors.Muted, 16);
        chevron.VerticalOptions = LayoutOptions.Center;

        var titleLabel = new Label
        {
            Text = title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = AppColors.Dark
        };

        var subtitleLabel = new Label
        {
            Text = subtitle,
            FontSize = 13,
            TextColor = AppColors.Muted
        };

        var textStack = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children = { titleLabel, subtitleLabel }
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(44)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(24))
            },
            ColumnSpacing = 14,
            Children = { iconFrame, textStack, chevron }
        };

        Grid.SetColumn(iconFrame, 0);
        Grid.SetColumn(textStack, 1);
        Grid.SetColumn(chevron, 2);

        var card = new Border
        {
            BackgroundColor = AppColors.Surface,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = new SolidColorBrush(AppColors.Border),
            StrokeThickness = 1,
            Padding = new Thickness(16),
            Content = grid
        };

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (_, _) => await Shell.Current.GoToAsync(route);
        card.GestureRecognizers.Add(tapGesture);

        return card;
    }

    public static View CreateSectionHeader(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = AppColors.Muted,
            Margin = new Thickness(4, 8, 0, 0)
        };
    }
}
