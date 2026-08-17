using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services.Import;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

public sealed class CashFlowProjectionTests
{
    private static async Task<(SqliteConnection Connection, ProjectionService Service, DbContextOptions<CoinStackDbContext> Options)> CreateServiceAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CoinStackDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new CoinStackDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        return (connection, new ProjectionService(new TestDbContextFactory(options)), options);
    }

    [Fact]
    public async Task BuildProjectionAsync_Excludes_Current_Incomplete_Month_From_Historical_Median()
    {
        var (connection, service, options) = await CreateServiceAsync();
        using (connection)
        {
            var now = DateTime.UtcNow;
            var currentMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var lastMonth = currentMonth.AddMonths(-1);
            var twoMonthsAgo = currentMonth.AddMonths(-2);

            await using (var db = new CoinStackDbContext(options))
            {
                db.AppSettings.Add(new AppSettings
                {
                    MonthlyIncome = 30000m,
                    Currency = "ZAR"
                });

                // Month 1: 2 months ago (complete)
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = twoMonthsAgo.AddDays(5),
                    Amount = 30000m,
                    Type = TransactionType.Income,
                    Description = "Salary Multicat"
                });
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = twoMonthsAgo.AddDays(10),
                    Amount = 14000m,
                    Type = TransactionType.Expense,
                    Description = "Living Expenses"
                });

                // Month 2: 1 month ago (complete)
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = lastMonth.AddDays(5),
                    Amount = 30000m,
                    Type = TransactionType.Income,
                    Description = "Salary Multicat"
                });
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = lastMonth.AddDays(10),
                    Amount = 16000m,
                    Type = TransactionType.Expense,
                    Description = "Living Expenses"
                });

                // Month 3: Current ongoing month (incomplete: salary not arrived yet, only 2k expenses)
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = currentMonth.AddDays(2),
                    Amount = 2000m,
                    Type = TransactionType.Expense,
                    Description = "Early groceries"
                });

                await db.SaveChangesAsync();
            }

            var projection = await service.BuildProjectionAsync();

            // History should contain all 3 months
            Assert.Equal(3, projection.History.Count);

            // Projected income and expenses should be based on the completed months (median of 30k is 30k, median of 14k and 16k is 15k)
            Assert.Equal(30000m, projection.ProjectedMonthlyIncome);
            Assert.Equal(15000m, projection.ProjectedMonthlyExpenses);
            Assert.Equal(15000m, projection.ProjectedMonthlyNet);
            Assert.Equal(50.0m, projection.SavingsRatePercent);
        }
    }

    [Fact]
    public async Task BuildProjectionAsync_Anchors_On_Detected_Salary_And_Ignores_Incomplete_Boundary_Months()
    {
        var (connection, service, options) = await CreateServiceAsync();
        using (connection)
        {
            await using (var db = new CoinStackDbContext(options))
            {
                // Exact scenario from user's 18 March - 18 June statement
                // March 25 Salary
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc),
                    Amount = 30413.13m,
                    Type = TransactionType.Income,
                    Description = "FNB OB Pmt Multicat Salary 51010036155"
                });

                // April 07 Pass-Through: 50,000 Multicat in, 47,000 to Pa out
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = new DateTime(2026, 4, 7, 0, 0, 0, DateTimeKind.Utc),
                    Amount = 50000m,
                    Type = TransactionType.Income,
                    Description = "FNB OB Pmt Multicat"
                });
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = new DateTime(2026, 4, 7, 0, 0, 0, DateTimeKind.Utc),
                    Amount = 47000m,
                    Type = TransactionType.Expense,
                    Description = "FNB App Payment To Pa Tiaan"
                });

                // April 24 Salary
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = new DateTime(2026, 4, 24, 0, 0, 0, DateTimeKind.Utc),
                    Amount = 30413.13m,
                    Type = TransactionType.Income,
                    Description = "FNB OB Pmt Multicat Salary 51010036155"
                });

                // May 25 Salary
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    Amount = 30413.13m,
                    Type = TransactionType.Income,
                    Description = "FNB OB Pmt Multicat Salary 51010036155"
                });

                // June: statement cut off on 18 June before 25 June payday.
                // Has normal expenses but 0 salary.
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = new DateTime(2026, 6, 4, 0, 0, 0, DateTimeKind.Utc),
                    Amount = 1343.63m,
                    Type = TransactionType.Expense,
                    Description = "POS Purchase Uitzicht Pharma 28"
                });

                await db.SaveChangesAsync();
            }

            var projection = await service.BuildProjectionAsync();

            // Projected income must anchor to the ~30,413.13 salary, NOT average down to 25,000 from the cutoff June!
            Assert.Equal(30413.13m, projection.ProjectedMonthlyIncome);
        }
    }

    [Fact]
    public async Task BuildProjectionAsync_Aggregates_Top_Merchants_Correctly()
    {
        var (connection, service, options) = await CreateServiceAsync();
        using (connection)
        {
            await using (var db = new CoinStackDbContext(options))
            {
                // OnlyFans variants
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                    Amount = 590.07m,
                    Type = TransactionType.Expense,
                    Description = "POS Purchase 34.50 Onlyfans*J 412752*5585 24 Apr"
                });
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                    Amount = 590.07m,
                    Type = TransactionType.Expense,
                    Description = "POS Purchase 34.50 Of 412752*5585 24 Apr"
                });

                // Steam variants
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = new DateTime(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc),
                    Amount = 310.10m,
                    Type = TransactionType.Expense,
                    Description = "POS Purchase 310.10 Steamgames.C 412752*5585 26 May"
                });
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc),
                    Amount = 500.00m,
                    Type = TransactionType.Expense,
                    Description = "Digital Content Voucher Steam 26053017560837750"
                });

                await db.SaveChangesAsync();
            }

            var projection = await service.BuildProjectionAsync();

            Assert.NotEmpty(projection.TopMerchants);

            var onlyfans = projection.TopMerchants.FirstOrDefault(m => m.MerchantName == "OnlyFans");
            Assert.NotNull(onlyfans);
            Assert.Equal(1180.14m, onlyfans.TotalAmount);
            Assert.Equal(2, onlyfans.TransactionCount);

            var steam = projection.TopMerchants.FirstOrDefault(m => m.MerchantName == "Steam");
            Assert.NotNull(steam);
            Assert.Equal(810.10m, steam.TotalAmount);
            Assert.Equal(2, steam.TransactionCount);
        }
    }

    [Fact]
    public async Task BuildProjectionAsync_Detects_Recurring_Items_On_Single_Statement()
    {
        var (connection, service, options) = await CreateServiceAsync();
        using (connection)
        {
            var now = DateTime.UtcNow;

            await using (var db = new CoinStackDbContext(options))
            {
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = now.AddDays(-15),
                    Amount = 199m,
                    Type = TransactionType.Expense,
                    Description = "Netflix Subscription"
                });
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = now.AddDays(-14),
                    Amount = 450m,
                    Type = TransactionType.Expense,
                    Description = "Virgin Active Gym"
                });
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = now.AddDays(-10),
                    Amount = 28000m,
                    Type = TransactionType.Income,
                    Description = "Monthly Salary Multicat"
                });

                await db.SaveChangesAsync();
            }

            var projection = await service.BuildProjectionAsync();

            Assert.NotEmpty(projection.RecurringExpenses);
            Assert.Contains(projection.RecurringExpenses, r => r.Description.Contains("Netflix") && r.AverageAmount == 199m);
            Assert.Contains(projection.RecurringExpenses, r => r.Description.Contains("Virgin Active") && r.AverageAmount == 450m);
            Assert.NotEmpty(projection.RecurringIncome);
            Assert.Contains(projection.RecurringIncome, r => r.Description.Contains("Salary") && r.AverageAmount == 28000m);
        }
    }

    [Fact]
    public async Task BuildProjectionAsync_Falls_Back_To_Settings_When_No_Historical_Income()
    {
        var (connection, service, options) = await CreateServiceAsync();
        using (connection)
        {
            await using (var db = new CoinStackDbContext(options))
            {
                db.AppSettings.Add(new AppSettings
                {
                    MonthlyIncome = 25000m,
                    Currency = "ZAR"
                });

                // Only expense transactions recorded
                db.Transactions.Add(new Transaction
                {
                    OccurredAtUtc = DateTime.UtcNow.AddMonths(-1).AddDays(5),
                    Amount = 8000m,
                    Type = TransactionType.Expense,
                    Description = "Rent"
                });

                await db.SaveChangesAsync();
            }

            var projection = await service.BuildProjectionAsync();

            Assert.Equal(25000m, projection.ProjectedMonthlyIncome);
            Assert.Equal(8000m, projection.ProjectedMonthlyExpenses);
            Assert.Equal(17000m, projection.ProjectedMonthlyNet);
        }
    }
}
