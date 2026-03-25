using CoinStack.Data.Entities;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Pages;

public sealed class CategoriesPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;
    private readonly VerticalStackLayout _categoryList;

    public CategoriesPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Categories";
        BackgroundColor = AppColors.Background;

        _categoryList = new VerticalStackLayout { Spacing = 8 };

        var addButton = new Button
        {
            Text = $"{AppIcons.GlyphPlus}  Add Category",
            FontFamily = "FontAwesomeSolid",
            BackgroundColor = AppColors.Dark,
            TextColor = AppColors.Surface,
            CornerRadius = 24,
            HeightRequest = 48,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14
        };
        addButton.Clicked += OnAddClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new Label { Text = "Categories", FontFamily = "SpaceGroteskBold", FontSize = 24, TextColor = AppColors.Dark },
                    new Label { Text = "Manage transaction categories for expenses and income.", FontSize = 14, TextColor = AppColors.Muted, FontFamily = "SpaceGroteskRegular" },
                    addButton,
                    _categoryList
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
            var categories = await _financeService.GetCategoriesAsync();
            _categoryList.Children.Clear();

            if (categories.Count == 0)
            {
                _categoryList.Children.Add(new Label { Text = "No categories yet.", FontFamily = "SpaceGroteskRegular", FontSize = 14, TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center });
                return;
            }

            foreach (var cat in categories)
            {
                var colorDot = new BoxView
                {
                    WidthRequest = 12,
                    HeightRequest = 12,
                    CornerRadius = 6,
                    Color = string.IsNullOrWhiteSpace(cat.ColorHex) ? AppColors.Muted : Color.FromArgb(cat.ColorHex),
                    VerticalOptions = LayoutOptions.Center
                };

                var nameLabel = new Label { Text = cat.Name, FontFamily = "SpaceGroteskBold", FontSize = 15, TextColor = AppColors.Dark, VerticalOptions = LayoutOptions.Center };
                var scopeLabel = new Label { Text = cat.Scope.ToString(), FontSize = 12, TextColor = AppColors.Muted, FontFamily = "SpaceGroteskRegular", VerticalOptions = LayoutOptions.Center };

                var deleteBtn = new Label
                {
                    Text = AppIcons.GlyphTrash,
                    FontFamily = "FontAwesomeSolid",
                    FontSize = 16,
                    TextColor = AppColors.Danger,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.End
                };
                var deleteTap = new TapGestureRecognizer();
                var catId = cat.Id;
                deleteTap.Tapped += async (_, _) =>
                {
                    var confirmed = await DisplayAlertAsync("Delete", $"Delete category '{cat.Name}'?", "Delete", "Cancel");
                    if (confirmed)
                    {
                        await _financeService.DeleteCategoryAsync(catId);
                        await LoadAsync();
                    }
                };
                deleteBtn.GestureRecognizers.Add(deleteTap);

                var grid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(new GridLength(20)),
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(new GridLength(60)),
                        new ColumnDefinition(new GridLength(30))
                    },
                    ColumnSpacing = 10,
                    Padding = new Thickness(0, 4)
                };

                Grid.SetColumn(colorDot, 0);
                Grid.SetColumn(nameLabel, 1);
                Grid.SetColumn(scopeLabel, 2);
                Grid.SetColumn(deleteBtn, 3);
                grid.Children.Add(colorDot);
                grid.Children.Add(nameLabel);
                grid.Children.Add(scopeLabel);
                grid.Children.Add(deleteBtn);

                var card = new Border
                {
                    BackgroundColor = AppColors.Surface,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Stroke = new SolidColorBrush(AppColors.Border),
                    StrokeThickness = 1,
                    Padding = new Thickness(16, 12),
                    Content = grid
                };

                _categoryList.Children.Add(card);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("New Category", "Category name:", "Add", "Cancel");
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            await _financeService.AddCategoryAsync(name, CategoryScope.Both);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
