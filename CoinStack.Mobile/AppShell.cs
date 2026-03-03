using CoinStack.Mobile.Pages;
using CoinStack.Mobile.Services;

namespace CoinStack.Mobile;

public sealed class AppShell : TabbedPage
{
    public AppShell(IMobileFinanceService financeService)
    {
        Title = "CoinStack";

        Children.Add(new NavigationPage(new DashboardPage(financeService))
        {
            Title = "Dashboard"
        });

        Children.Add(new NavigationPage(new TransactionsPage(financeService))
        {
            Title = "Transactions"
        });

        Children.Add(new NavigationPage(new BucketsPage(financeService))
        {
            Title = "Buckets"
        });

        Children.Add(new NavigationPage(new GoalsPage(financeService))
        {
            Title = "Goals"
        });

        Children.Add(new NavigationPage(new SavingsPage(financeService))
        {
            Title = "Savings"
        });

        Children.Add(new NavigationPage(new SubscriptionsPage(financeService))
        {
            Title = "Subscriptions"
        });

        Children.Add(new NavigationPage(new DebtPage(financeService))
        {
            Title = "Debt"
        });

        Children.Add(new NavigationPage(new ReflectionsPage(financeService))
        {
            Title = "Reflections"
        });

        Children.Add(new NavigationPage(new ReportsPage(financeService))
        {
            Title = "Reports"
        });

        Children.Add(new NavigationPage(new SettingsPage(financeService))
        {
            Title = "Settings"
        });
    }
}
