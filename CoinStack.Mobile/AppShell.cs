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

        Children.Add(new NavigationPage(new SettingsPage(financeService))
        {
            Title = "Settings"
        });
    }
}
