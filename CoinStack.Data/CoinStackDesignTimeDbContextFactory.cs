using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CoinStack.Data;

public sealed class CoinStackDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CoinStackDbContext>
{
    public CoinStackDbContext CreateDbContext(string[] args)
    {
        var provider = (Environment.GetEnvironmentVariable("FINANCEMANAGER_DB_PROVIDER") ?? "sqlite")
            .Trim()
            .ToLowerInvariant();

        var connectionString =
            Environment.GetEnvironmentVariable("FINANCEMANAGER_CONNECTIONSTRING")
            ?? (provider is "postgresql" or "postgres" or "npgsql"
                ? "Host=localhost;Port=5432;Database=coinstack;Username=postgres;Password=postgres"
                : "Data Source=financemanager.db");

        var optionsBuilder = new DbContextOptionsBuilder<CoinStackDbContext>();

        if (provider is "postgresql" or "postgres" or "npgsql")
        {
            optionsBuilder.UseNpgsql(connectionString);
        }
        else
        {
            optionsBuilder.UseSqlite(connectionString);
        }

        return new CoinStackDbContext(optionsBuilder.Options);
    }
}
