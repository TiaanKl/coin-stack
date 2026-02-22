namespace CoinStack.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFinanceManagerAppServices(this IServiceCollection services)
    {
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IGoalService, GoalService>();
        services.AddScoped<IDebtService, DebtService>();
        services.AddScoped<IDebtCalculatorEngine, DebtCalculatorEngine>();
        services.AddScoped<IDataResetService, DataResetService>();
        services.AddScoped<IBucketService, BucketService>();
        services.AddScoped<IScoringService, ScoringService>();
        services.AddScoped<IReflectionService, ReflectionService>();
        services.AddScoped<IGameLoopService, GameLoopService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IWaitlistService, WaitlistService>();
        services.AddScoped<ISavingsService, SavingsService>();

        return services;
    }
}
