using CoinStack.Data;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoinStackDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
