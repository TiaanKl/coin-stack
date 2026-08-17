// File: CoinStack.Desktop/ViewModels/CategoriesViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class CategoriesViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<CategoryItem> _categories;
    
    public CategoriesViewModel()
    {
        Categories = new ObservableCollection<CategoryItem>
        {
            new() { Name = "Groceries", Color = "#10B981", Scope = "Expense" },
            new() { Name = "Salary", Color = "#6366F1", Scope = "Income" },
            new() { Name = "Entertainment", Color = "#F59E0B", Scope = "Expense" },
            new() { Name = "Transfer", Color = "#8B5CF6", Scope = "Transfer" }
        };
    }
}

public class CategoryItem
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public string Scope { get; set; } = "";
}