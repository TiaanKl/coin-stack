using FinanceManager.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Services;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceManagerDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
