using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    public SettingsService(IDbContextFactory<CoinStackDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
               ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.AppSettings.FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            db.AppSettings.Add(settings);
        }
        else
        {
            existing.Currency = settings.Currency;
            existing.MonthStartDay = settings.MonthStartDay;
            existing.MonthlyIncome = settings.MonthlyIncome;
            existing.EnableScoring = settings.EnableScoring;
            existing.EnableStreaks = settings.EnableStreaks;
            existing.EnableToast = settings.EnableToast;
            existing.EnableSounds = settings.EnableSounds;
            existing.EnableReflections = settings.EnableReflections;
            existing.LargeExpenseThreshold = settings.LargeExpenseThreshold;
            existing.SavingsIsPercent = settings.SavingsIsPercent;
            existing.MonthlySavingsAmount = settings.MonthlySavingsAmount;
            existing.MonthlySavingsPercent = settings.MonthlySavingsPercent;
            existing.SavingsInterestRate = settings.SavingsInterestRate;
            existing.SavingsInterestIsYearly = settings.SavingsInterestIsYearly;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
