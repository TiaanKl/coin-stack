using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    public SubscriptionService(IDbContextFactory<CoinStackDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Subscription>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Subscriptions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Subscription?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Subscription> CreateAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    public async Task UpdateAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.Subscriptions.FirstOrDefaultAsync(x => x.Id == subscription.Id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.Name = subscription.Name;
        existing.Category = subscription.Category;
        existing.Cycle = subscription.Cycle;
        existing.Cost = subscription.Cost;
        existing.Status = subscription.Status;
        existing.ColorHex = subscription.ColorHex;
        existing.CustomHexColor = subscription.CustomHexColor;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Subscriptions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        db.Subscriptions.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }
}
