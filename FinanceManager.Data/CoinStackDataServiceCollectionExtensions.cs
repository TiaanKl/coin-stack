using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoinStack.Data;

public static class CoinStackDataServiceCollectionExtensions
{
    public static IServiceCollection AddFinanceManagerData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<CoinStackDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        services.AddScoped(sp =>
        {
            var factory = sp.GetRequiredService<IDbContextFactory<CoinStackDbContext>>();
            return factory.CreateDbContext();
        });

        return services;
    }

    public static IServiceCollection AddFinanceManagerData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("FinanceManager");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Data Source=financemanager.db";
        }

        return services.AddFinanceManagerData(connectionString);
    }
}
