using CoinStack.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Data;

public sealed class CoinStackDbContext : IdentityDbContext<ApplicationUser>
{
    public CoinStackDbContext(DbContextOptions<CoinStackDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<DebtAccount> DebtAccounts => Set<DebtAccount>();
    public DbSet<Bucket> Buckets => Set<Bucket>();
    public DbSet<ScoreEvent> ScoreEvents => Set<ScoreEvent>();
    public DbSet<Streak> Streaks => Set<Streak>();
    public DbSet<Reflection> Reflections => Set<Reflection>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<WaitlistItem> WaitlistItems => Set<WaitlistItem>();
    public DbSet<SavingsState> SavingsState => Set<SavingsState>();
    public DbSet<SavingsMonthlySummary> SavingsMonthlySummaries => Set<SavingsMonthlySummary>();
    public DbSet<SavingsFallbackEvent> SavingsFallbackEvents => Set<SavingsFallbackEvent>();
    public DbSet<CbtJournalEntry> CbtJournalEntries => Set<CbtJournalEntry>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserLevel> UserLevels => Set<UserLevel>();
    public DbSet<DailyChallenge> DailyChallenges => Set<DailyChallenge>();
    public DbSet<WeeklyRecap> WeeklyRecaps => Set<WeeklyRecap>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoinStackDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        TouchUpdatedTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TouchUpdatedTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void TouchUpdatedTimestamps()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = utcNow;
                entry.Entity.UpdatedAtUtc = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = utcNow;
            }
        }
    }
}
