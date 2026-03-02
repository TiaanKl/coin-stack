using CoinStack.Data;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class DataResetService : IDataResetService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    public DataResetService(IDbContextFactory<CoinStackDbContext> dbFactory)
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