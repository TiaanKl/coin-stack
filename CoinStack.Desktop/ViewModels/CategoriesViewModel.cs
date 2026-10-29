using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoinStack.Desktop.ViewModels;

public partial class CategoriesViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ICategoryService _categories;

    [ObservableProperty] private string _newCategoryName = "";
    [ObservableProperty] private CategoryScope _newScope = CategoryScope.Both;
    [ObservableProperty] private int _categoryCount;

    public ObservableCollection<CategoryRow> Categories { get; } = [];
    public ObservableCollection<CategoryScope> ScopeOptions { get; } =
        [CategoryScope.Both, CategoryScope.Expense, CategoryScope.Income, CategoryScope.Transfer];

    public CategoriesViewModel(ICategoryService categories)
    {
        _categories = categories;
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var list = await _categories.GetAllAsync();
            Categories.Clear();
            foreach (var cat in list.OrderBy(c => c.Name))
            {
                Categories.Add(new CategoryRow
                {
                    Id = cat.Id,
                    Name = cat.Name,
                    ScopeText = cat.Scope.ToString()
                });
            }

            CategoryCount = Categories.Count;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load categories: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            StatusMessage = "Enter a category name.";
            return;
        }

        IsBusy = true;
        try
        {
            await _categories.CreateAsync(new Category
            {
                Name = NewCategoryName.Trim(),
                Scope = NewScope
            });

            NewCategoryName = "";
            NewScope = CategoryScope.Both;
            StatusMessage = "Category created.";
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not create category: {ex.Message}";
            IsBusy = false;
        }
    }
}
