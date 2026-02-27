using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoinStack.Data;

public static class CoinStackDataServiceCollectionExtensions
{
    private const string SqliteProvider = "sqlite";
    private const string PostgreSqlProvider = "postgresql";

    public static IServiceCollection AddFinanceManagerData(
        this IServiceCollection services,
        string connectionString,
        string provider)
    {
        var normalizedProvider = provider.Trim().ToLowerInvariant();

        services.AddDbContextFactory<CoinStackDbContext>(options =>
        {
            if (normalizedProvider is PostgreSqlProvider or "postgres" or "npgsql")
            {
                options.UseNpgsql(connectionString);
                return;
            }

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
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var normalizedProvider = provider.Trim().ToLowerInvariant();

        var connectionString = normalizedProvider is PostgreSqlProvider or "postgres" or "npgsql"
            ? configuration.GetConnectionString("FinanceManagerPostgres")
            : configuration.GetConnectionString("FinanceManagerSqlite");

        if (string.IsNullOrWhiteSpace(connectionString) && normalizedProvider is PostgreSqlProvider or "postgres" or "npgsql")
        {
            connectionString = "Host=localhost;Port=5432;Database=coinstack;Username=postgres;Password=postgres";
        }

        if (string.IsNullOrWhiteSpace(connectionString) && normalizedProvider == SqliteProvider)
        {
            connectionString = "Data Source=financemanager.db";
        }

        return services.AddFinanceManagerData(connectionString ?? "Data Source=financemanager.db", provider);
    }
}
