using CoinStack.Data.Entities;
using CoinStack.Services;
using CoinStack.Services.Import;
using CoinStack.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

public sealed class RecurringMerchantDetectorTests
{
    [Fact]
    public void Two_Monthly_Netflix_Rows_Become_A_Subscription_Proposal()
    {
        var candidates = new List<ImportCandidate>
        {
            Netflix(new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc), 199.00m),
            Netflix(new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc), 199.00m)
        };

        var proposals = RecurringMerchantDetector.Detect(candidates);

        var netflix = Assert.Single(proposals);
        Assert.Equal("NETFLIX", netflix.NormalizedKey);
        Assert.Equal(199.00m, netflix.MedianAmount);
        Assert.Equal(SubscriptionCycle.Monthly, netflix.Cycle);
        Assert.Equal(12, netflix.DebitOrderDay);
        Assert.True(netflix.CreateSubscription);
        Assert.True(netflix.CreateBucket);
    }

    [Fact]
    public void Salary_Income_Is_Never_A_Subscription()
    {
        var txs = new List<Transaction>
        {
            new()
            {
                Type = TransactionType.Income,
                Description = "Salary (Multicat)",
                Amount = 25000m,
                OccurredAtUtc = new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                Type = TransactionType.Income,
                Description = "Salary (Multicat)",
                Amount = 25000m,
                OccurredAtUtc = new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        var proposals = RecurringMerchantDetector.DetectFromTransactions(txs);
        Assert.Empty(proposals);
    }

    [Fact]
    public void Grocery_Pos_Does_Not_Become_A_Subscription()
    {
        var candidates = new List<ImportCandidate>
        {
            Grocery(new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), 412m),
            Grocery(new DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Utc), 380m),
            Grocery(new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), 450m),
            Grocery(new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc), 390m)
        };

        Assert.Empty(RecurringMerchantDetector.Detect(candidates));
    }

    [Fact]
    public void AppSettings_Default_MonthlyIncome_Is_Zero()
    {
        Assert.Equal(0m, new AppSettings().MonthlyIncome);
    }

    [Fact]
    public void Synthetic_Monthly_Income_Is_Identified()
    {
        // Shape of the rows the retired auto-income feature generated: period start at midnight UTC.
        var tx = new Transaction
        {
            Type = TransactionType.Income,
            Description = TransactionConventions.SyntheticMonthlyIncomeDescription,
            Source = TransactionConventions.AutoSource,
            Amount = 5000m,
            OccurredAtUtc = new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc)
        };

        Assert.True(TransactionConventions.IsSyntheticMonthlyIncome(tx));
    }

    [Fact]
    public void Manual_Income_Named_Monthly_Income_Is_Not_Treated_As_Synthetic()
    {
        var logged = new Transaction
        {
            Type = TransactionType.Income,
            Description = TransactionConventions.SyntheticMonthlyIncomeDescription,
            Amount = 5000m,
            OccurredAtUtc = new DateTime(2026, 3, 25, 14, 32, 7, DateTimeKind.Utc)
        };

        var categorised = new Transaction
        {
            Type = TransactionType.Income,
            Description = TransactionConventions.SyntheticMonthlyIncomeDescription,
            Amount = 5000m,
            CategoryId = 9,
            OccurredAtUtc = new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc)
        };

        Assert.False(TransactionConventions.IsSyntheticMonthlyIncome(logged));
        Assert.False(TransactionConventions.IsSyntheticMonthlyIncome(categorised));
    }

    private static ImportCandidate Netflix(DateTime date, decimal amount) => new()
    {
        DateUtc = date,
        Amount = amount,
        Type = TransactionType.Expense,
        Description = "Netflix",
        MerchantName = "Netflix",
        NormalizedKey = "NETFLIX",
        CategoryName = "Entertainment"
    };

    private static ImportCandidate Grocery(DateTime date, decimal amount) => new()
    {
        DateUtc = date,
        Amount = amount,
        Type = TransactionType.Expense,
        Description = "Pick n Pay",
        MerchantName = "Pick n Pay",
        NormalizedKey = "PICK_N_PAY",
        CategoryName = "Groceries"
    };

    [Fact]
    public void Fourteen_Day_Gaps_Are_Fortnightly_Not_Monthly()
    {
        var candidates = new[] { 2, 16, 30, 44, 58 }
            .Select(day => Row("GYM", "Gym", "Health", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(day - 1), 150m))
            .ToList();

        var proposal = Assert.Single(RecurringMerchantDetector.Detect(candidates));

        Assert.Equal(SubscriptionCycle.Fortnightly, proposal.Cycle);
        Assert.Equal(150m, proposal.MedianAmount);
        Assert.Equal(325m, proposal.MonthlyAllocation);
    }

    [Fact]
    public void Six_Monthly_Gaps_Are_Half_Yearly_Not_Monthly()
    {
        var candidates = new List<ImportCandidate>
        {
            Row("CAR_LICENCE", "Car licence", "Transport", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc), 600m),
            Row("CAR_LICENCE", "Car licence", "Transport", new DateTime(2025, 7, 9, 0, 0, 0, DateTimeKind.Utc), 600m),
            Row("CAR_LICENCE", "Car licence", "Transport", new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), 600m)
        };

        var proposal = Assert.Single(RecurringMerchantDetector.Detect(candidates));

        Assert.Equal(SubscriptionCycle.HalfYearly, proposal.Cycle);
        Assert.Equal(100m, proposal.MonthlyAllocation);
    }

    [Fact]
    public void Two_Charges_A_Month_Cost_The_Monthly_Total()
    {
        var candidates = new List<ImportCandidate>();
        for (var month = 3; month <= 5; month++)
        {
            candidates.Add(Row("ONLYFANS", "OnlyFans", "Entertainment", new DateTime(2026, month, 5, 0, 0, 0, DateTimeKind.Utc), 100m));
            candidates.Add(Row("ONLYFANS", "OnlyFans", "Entertainment", new DateTime(2026, month, 8, 0, 0, 0, DateTimeKind.Utc), 100m));
        }

        var proposal = Assert.Single(RecurringMerchantDetector.Detect(candidates));

        Assert.Equal(SubscriptionCycle.Monthly, proposal.Cycle);
        Assert.Equal(200m, proposal.MedianAmount);
        Assert.Equal(200m, proposal.MonthlyAllocation);
    }

    [Fact]
    public async Task Scan_Keeps_A_Cost_The_User_Edited()
    {
        var (connection, options) = await CreateDatabaseAsync();
        await using (connection)
        {
            await using (var db = new CoinStackDbContext(options))
            {
                var entertainment = new Category { Name = "Entertainment", Scope = CategoryScope.Expense };
                db.Categories.Add(entertainment);
                db.Subscriptions.Add(new Subscription { Name = "Netflix", Category = "Entertainment", Cost = 250m, DebitOrderDay = 20 });
                db.Transactions.AddRange(NetflixExpense(3, entertainment), NetflixExpense(4, entertainment));
                await db.SaveChangesAsync();
            }

            var service = new BankStatementImportService(new TestDbContextFactory(options), new FnbPdfBankFeed());
            await service.ScanAndCreateRecurringAsync();

            await using var check = new CoinStackDbContext(options);
            var netflix = await check.Subscriptions.SingleAsync();
            Assert.Equal(250m, netflix.Cost);
            Assert.Equal(20, netflix.DebitOrderDay);
            Assert.All(await check.Transactions.ToListAsync(), t => Assert.Equal(netflix.Id, t.SubscriptionId));
        }
    }

    [Fact]
    public async Task Scan_Does_Not_Recreate_A_Deleted_Subscription_Or_Bucket()
    {
        var (connection, options) = await CreateDatabaseAsync();
        await using (connection)
        {
            await using (var db = new CoinStackDbContext(options))
            {
                var entertainment = new Category { Name = "Entertainment", Scope = CategoryScope.Expense };
                db.Categories.Add(entertainment);
                db.Transactions.AddRange(NetflixExpense(3, entertainment), NetflixExpense(4, entertainment));
                await db.SaveChangesAsync();
            }

            var factory = new TestDbContextFactory(options);
            var importService = new BankStatementImportService(factory, new FnbPdfBankFeed());
            await importService.ScanAndCreateRecurringAsync();

            int subscriptionId, bucketId;
            await using (var db = new CoinStackDbContext(options))
            {
                subscriptionId = (await db.Subscriptions.SingleAsync()).Id;
                bucketId = (await db.Buckets.SingleAsync()).Id;
            }

            await new SubscriptionService(factory).DeleteAsync(subscriptionId);
            await new BucketService(factory).DeleteAsync(bucketId);

            await importService.ScanAndCreateRecurringAsync();

            await using var check = new CoinStackDbContext(options);
            Assert.Empty(await check.Subscriptions.ToListAsync());
            Assert.Empty(await check.Buckets.ToListAsync());
        }
    }

    private static async Task<(SqliteConnection Connection, DbContextOptions<CoinStackDbContext> Options)> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CoinStackDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new CoinStackDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return (connection, options);
    }

    private static Transaction NetflixExpense(int month, Category category) => new()
    {
        Type = TransactionType.Expense,
        Description = "Netflix",
        Amount = 199m,
        CategoryId = category.Id,
        Category = category,
        OccurredAtUtc = new DateTime(2026, month, 12, 0, 0, 0, DateTimeKind.Utc)
    };

    private static ImportCandidate Row(string key, string name, string category, DateTime date, decimal amount) => new()
    {
        DateUtc = date,
        Amount = amount,
        Type = TransactionType.Expense,
        Description = name,
        MerchantName = name,
        NormalizedKey = key,
        CategoryName = category
    };

    [Fact]
    public async Task Merged_Files_Keep_Recurring_Proposals_And_Revert_Removes_What_The_Import_Created()
    {
        var (connection, options) = await CreateDatabaseAsync();
        await using (connection)
        {
            var service = new BankStatementImportService(new TestDbContextFactory(options), new FnbPdfBankFeed());

            var march = PreviewWith(NetflixCandidate(3, "fp-march"));
            var april = PreviewWith(NetflixCandidate(4, "fp-april"));

            var merged = await service.MergePreviewsAsync([march, april]);

            var proposal = Assert.Single(merged.RecurringProposals);
            Assert.Equal("NETFLIX", proposal.NormalizedKey);

            var result = await service.ImportAsync(merged);
            Assert.Equal(1, result.SubscriptionsCreated);
            Assert.Equal(1, result.BucketsCreated);

            await using (var db = new CoinStackDbContext(options))
            {
                var subscription = await db.Subscriptions.SingleAsync();
                Assert.Equal(result.ImportId, subscription.CreatedByStatementImportId);
                Assert.All(await db.Transactions.ToListAsync(), t => Assert.Equal(subscription.Id, t.SubscriptionId));
            }

            var reverted = await service.RevertImportAsync(result.ImportId);

            Assert.Equal(new StatementRevertResult(2, 1, 1), reverted);
            await using var check = new CoinStackDbContext(options);
            Assert.Empty(await check.Transactions.ToListAsync());
            Assert.Empty(await check.Subscriptions.ToListAsync());
            Assert.Empty(await check.Buckets.ToListAsync());
        }
    }

    private static StatementImportPreview PreviewWith(ImportCandidate candidate)
    {
        var preview = new StatementImportPreview { BankFormat = "FNB", FileName = $"{candidate.Fingerprint}.pdf" };
        preview.Candidates.Add(candidate);
        return preview;
    }

    private static ImportCandidate NetflixCandidate(int month, string fingerprint) => new()
    {
        DateUtc = new DateTime(2026, month, 12, 0, 0, 0, DateTimeKind.Utc),
        Amount = 199m,
        Type = TransactionType.Expense,
        Description = "Netflix",
        MerchantName = "Netflix",
        NormalizedKey = "NETFLIX",
        CategoryName = "Entertainment",
        Fingerprint = fingerprint
    };
}
