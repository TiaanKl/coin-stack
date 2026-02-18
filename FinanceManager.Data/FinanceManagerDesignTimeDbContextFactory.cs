using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinanceManager.Data;

public sealed class FinanceManagerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<FinanceManagerDbContext>
{
    public FinanceManagerDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("FINANCEMANAGER_CONNECTIONSTRING")
            ?? "Data Source=financemanager.db";

        var optionsBuilder = new DbContextOptionsBuilder<FinanceManagerDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        return new FinanceManagerDbContext(optionsBuilder.Options);
    }
}
