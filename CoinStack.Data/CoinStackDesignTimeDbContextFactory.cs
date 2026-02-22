using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CoinStack.Data;

public sealed class CoinStackDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CoinStackDbContext>
{
    public CoinStackDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("FINANCEMANAGER_CONNECTIONSTRING")
            ?? "Data Source=financemanager.db";

        var optionsBuilder = new DbContextOptionsBuilder<CoinStackDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        return new CoinStackDbContext(optionsBuilder.Options);
    }
}
