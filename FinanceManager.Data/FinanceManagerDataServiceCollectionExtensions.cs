using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Data;

public static class FinanceManagerDataServiceCollectionExtensions
{
    public static IServiceCollection AddFinanceManagerData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<FinanceManagerDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        services.AddScoped(sp =>
        {
            var factory = sp.GetRequiredService<IDbContextFactory<FinanceManagerDbContext>>();
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
