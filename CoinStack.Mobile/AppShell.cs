using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Pages;

namespace CoinStack.Mobile;

public sealed class AppShell : Shell
{
    public AppShell()
    {
        Title = "CoinStack";

        // Register sub-page routes (navigated via GoToAsync from hub pages)
        Routing.RegisterRoute("transactions", typeof(TransactionsPage));
        Routing.RegisterRoute("income", typeof(IncomePage));
        Routing.RegisterRoute("buckets", typeof(BucketsPage));
        Routing.RegisterRoute("subscriptions", typeof(SubscriptionsPage));
        Routing.RegisterRoute("categories", typeof(CategoriesPage));
        Routing.RegisterRoute("goals", typeof(GoalsPage));
        Routing.RegisterRoute("savings", typeof(SavingsPage));
        Routing.RegisterRoute("debt", typeof(DebtPage));
        Routing.RegisterRoute("debt-simulator", typeof(DebtSimulatorPage));
        Routing.RegisterRoute("fallback-history", typeof(FallbackHistoryPage));
        Routing.RegisterRoute("challenges", typeof(ChallengesPage));
        Routing.RegisterRoute("achievements", typeof(AchievementsPage));
        Routing.RegisterRoute("reflections", typeof(ReflectionsPage));
        Routing.RegisterRoute("cbt-journal", typeof(CbtJournalPage));
        Routing.RegisterRoute("waitlist", typeof(WaitlistPage));
        Routing.RegisterRoute("weekly-recap", typeof(WeeklyRecapMobilePage));
        Routing.RegisterRoute("reports", typeof(ReportsPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));

        var tabBar = new TabBar();

        // Home tab
        var homeTab = new Tab { Title = "Home", Icon = AppIcons.HomeTab };
        homeTab.Items.Add(new ShellContent
        {
            ContentTemplate = new DataTemplate(typeof(DashboardPage))
        });
        tabBar.Items.Add(homeTab);

        // Money tab
        var moneyTab = new Tab { Title = "Money", Icon = AppIcons.WalletTab };
        moneyTab.Items.Add(new ShellContent
        {
            ContentTemplate = new DataTemplate(typeof(MoneyHubPage))
        });
        tabBar.Items.Add(moneyTab);

        // Goals tab
        var goalsTab = new Tab { Title = "Goals", Icon = AppIcons.FlagTab };
        goalsTab.Items.Add(new ShellContent
        {
            ContentTemplate = new DataTemplate(typeof(GoalsHubPage))
        });
        tabBar.Items.Add(goalsTab);

        // Growth tab
        var growthTab = new Tab { Title = "Growth", Icon = AppIcons.TrophyTab };
        growthTab.Items.Add(new ShellContent
        {
            ContentTemplate = new DataTemplate(typeof(GrowthHubPage))
        });
        tabBar.Items.Add(growthTab);

        // More tab
        var moreTab = new Tab { Title = "More", Icon = AppIcons.MoreTab };
        moreTab.Items.Add(new ShellContent
        {
            ContentTemplate = new DataTemplate(typeof(MoreHubPage))
        });
        tabBar.Items.Add(moreTab);

        Items.Add(tabBar);

        // Shell tab bar styling
        Shell.SetTabBarBackgroundColor(this, AppColors.Surface);
        Shell.SetTabBarUnselectedColor(this, AppColors.TabUnselected);
        Shell.SetTabBarTitleColor(this, AppColors.Dark);
        Shell.SetTabBarForegroundColor(this, AppColors.Dark);
        Shell.SetNavBarHasShadow(this, false);
        Shell.SetBackgroundColor(this, AppColors.Surface);
        Shell.SetForegroundColor(this, AppColors.Dark);

        FlyoutBehavior = FlyoutBehavior.Disabled;
    }
}
