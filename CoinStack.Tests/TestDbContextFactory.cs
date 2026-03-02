using CoinStack.Data;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Tests;

internal sealed class TestDbContextFactory : IDbContextFactory<CoinStackDbContext>
{
    private readonly DbContextOptions<CoinStackDbContext> _options;

    public TestDbContextFactory(DbContextOptions<CoinStackDbContext> options)
    {
        _options = options;
    }

    public CoinStackDbContext CreateDbContext()
    {
        return new CoinStackDbContext(_options);
    }

    public Task<CoinStackDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDbContext());
    }
}
