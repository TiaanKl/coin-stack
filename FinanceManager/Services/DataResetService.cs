using FinanceManager.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Services;

public sealed class DataResetService : IDataResetService
{
    private readonly IDbContextFactory<FinanceManagerDbContext> _dbFactory;

    public DataResetService(IDbContextFactory<FinanceManagerDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task ResetAllDataAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureDeletedAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
    }
}