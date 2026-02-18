using FinanceManager.Data;
using FinanceManager.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Services;

public sealed class BucketService : IBucketService
{
    private readonly IDbContextFactory<FinanceManagerDbContext> _dbFactory;

    public BucketService(IDbContextFactory<FinanceManagerDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Bucket>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Buckets
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Bucket?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Buckets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Bucket> CreateAsync(Bucket bucket, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.Buckets.Add(bucket);
        await db.SaveChangesAsync(cancellationToken);
        return bucket;
    }

    public async Task UpdateAsync(Bucket bucket, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.Buckets.FirstOrDefaultAsync(x => x.Id == bucket.Id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.Name = bucket.Name;
        existing.AllocatedAmount = bucket.AllocatedAmount;
        existing.ColorHex = bucket.ColorHex;
        existing.Icon = bucket.Icon;
        existing.SortOrder = bucket.SortOrder;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Buckets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        db.Buckets.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Dictionary<int, decimal>> GetSpentAmountsAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var startUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = startUtc.AddMonths(1);

        return await db.Transactions
            .AsNoTracking()
            .Where(t => t.BucketId != null
                        && t.Type == TransactionType.Expense
                        && t.OccurredAtUtc >= startUtc
                        && t.OccurredAtUtc < endUtc)
            .GroupBy(t => t.BucketId!.Value)
            .Select(g => new { BucketId = g.Key, Total = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.BucketId, x => x.Total, cancellationToken);
    }
}
