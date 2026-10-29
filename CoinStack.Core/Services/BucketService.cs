using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class BucketService : IBucketService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    public BucketService(IDbContextFactory<CoinStackDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Bucket>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Buckets
            .AsNoTracking()
            .Include(x => x.Category)
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
        existing.CategoryId = bucket.CategoryId;
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

    public async Task<Dictionary<int, decimal>> GetSpentAmountsForPeriodAsync(int monthStartDay, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        if (monthStartDay is < 1 or > 28) monthStartDay = 1;

        var startThisMonth = new DateTime(utcNow.Year, utcNow.Month, monthStartDay, 0, 0, 0, DateTimeKind.Utc);
        var startUtc = utcNow.Day >= monthStartDay ? startThisMonth : startThisMonth.AddMonths(-1);
        var endUtc = startUtc.AddMonths(1);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

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
