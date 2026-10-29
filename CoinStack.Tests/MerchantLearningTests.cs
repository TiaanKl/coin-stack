using System.Text;
using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CoinStack.Services.Import;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

/// <summary>Merchant matching on whole words, and categories learned from the person's own changes.</summary>
public sealed class MerchantLearningTests
{
    [Theory]
    [InlineData("City Of Cape Town Rates 2026", "ONLYFANS")]
    [InlineData("POS Purchase MR DIY Sandton 412752*5585 02 Jun", "MR_D_FOOD")]
    [InlineData("POS Purchase Auto Spares Centurion 412752*5585 02 Jun", "SPAR")]
    [InlineData("Steamboat Grill", "STEAM")]
    public void Words_Inside_Other_Words_Do_Not_Match(string description, string wrongKey)
    {
        Assert.NotEqual(wrongKey, MerchantNormalizer.Normalize(description, TransactionType.Expense).NormalizedKey);
    }

    [Theory]
    [InlineData("POS Purchase Spar Express 412752*5585 02 Jun", "SPAR", "Groceries")]
    [InlineData("POS Purchase Mr D Food 412752*5585 02 Jun", "MR_D_FOOD", "Dining")]
    [InlineData("POS Purchase Uber Eats 412752*5585 02 Jun", "UBER_EATS", "Dining")]
    [InlineData("POS Purchase Uber Trip 412752*5585 02 Jun", "UBER", "Transport")]
    [InlineData("POS Purchase Engen Rivonia 412752*5585 02 Jun", "FUEL", "Transport")]
    [InlineData("DebiCheck Vodacom 0821234567", "VODACOM", "Utilities")]
    public void Common_Merchants_Are_Recognised_And_The_Longest_Match_Wins(string description, string key, string category)
    {
        var merchant = MerchantNormalizer.Normalize(description, TransactionType.Expense);

        Assert.Equal(key, merchant.NormalizedKey);
        Assert.Equal(category, merchant.CategoryName);
    }

    [Fact]
    public void Salary_Is_Just_Salary_And_Personal_Payees_Are_Not_Built_In()
    {
        Assert.Equal("Salary", MerchantNormalizer.Normalize("FNB OB Pmt Acme Salary", TransactionType.Income).CanonicalName);
        Assert.False(MerchantNormalizer.IsKnownKey(MerchantNormalizer.Normalize("Payment From Pa", TransactionType.Income).NormalizedKey));
    }

    [Fact]
    public async Task Moving_A_Merchant_In_The_Preview_Is_Remembered_For_The_Next_Import()
    {
        await using var data = await TestData.CreateAsync();
        var service = data.ImportService();

        var first = await service.BuildPreviewAsync(Csv(("2026/07/03", "BOOKWORM ROSEBANK", -250m), ("2026/07/17", "BOOKWORM ROSEBANK", -120m)), "july.csv");
        var bookworm = Assert.Single(first!.GroupedMerchants, g => g.NormalizedKey == "BOOKWORM_ROSEBANK");
        Assert.Equal("Shopping", bookworm.CategoryName);
        Assert.False(bookworm.CategoryRemembered);

        MoveGroup(bookworm, "Entertainment");
        Assert.True(bookworm.WillRemember);
        await service.ImportAsync(first);

        var second = await service.BuildPreviewAsync(Csv(("2026/08/05", "BOOKWORM ROSEBANK", -90m), ("2026/08/09", "BOOKWORM MENLYN", -60m)), "august.csv");

        var sameShop = Assert.Single(second!.GroupedMerchants, g => g.NormalizedKey == "BOOKWORM_ROSEBANK");
        Assert.Equal("Entertainment", sameShop.CategoryName);
        Assert.True(sameShop.CategoryRemembered);

        // Another branch of a shop the app doesn't know follows the same choice.
        var otherBranch = Assert.Single(second.GroupedMerchants, g => g.NormalizedKey == "BOOKWORM_MENLYN");
        Assert.Equal("Entertainment", otherBranch.CategoryName);
    }

    [Fact]
    public async Task Changing_One_Row_Of_A_Merchant_Is_A_One_Off()
    {
        await using var data = await TestData.CreateAsync();
        var service = data.ImportService();

        var preview = await service.BuildPreviewAsync(Csv(("2026/07/03", "BOOKWORM ROSEBANK", -250m), ("2026/07/17", "BOOKWORM ROSEBANK", -120m)), "july.csv");
        var group = Assert.Single(preview!.GroupedMerchants, g => g.NormalizedKey == "BOOKWORM_ROSEBANK");
        group.Items[0].CategoryName = "Entertainment";

        Assert.False(group.WillRemember);
        await service.ImportAsync(preview);

        await using var db = data.NewContext();
        Assert.Empty(await db.MerchantRules.ToListAsync());
    }

    [Fact]
    public async Task Editing_A_Bank_Transaction_Teaches_The_Merchant_And_Offers_To_Move_Its_Others()
    {
        await using var data = await TestData.CreateAsync();
        var service = data.ImportService();
        var preview = await service.BuildPreviewAsync(
            Csv(("2026/07/03", "BOOKWORM ROSEBANK", -250m), ("2026/07/17", "BOOKWORM ROSEBANK", -120m), ("2026/07/28", "BOOKWORM ROSEBANK", -80m)),
            "july.csv");
        await service.ImportAsync(preview!);

        var transactions = new TransactionService(data.Factory, new FakeGameLoopService());
        var entertainmentId = await data.CategoryIdAsync("Entertainment");
        var edited = (await transactions.GetAllAsync()).First(t => t.Description.StartsWith("BOOKWORM"));
        edited.CategoryId = entertainmentId;
        await transactions.UpdateAsync(edited);

        await using (var db = data.NewContext())
        {
            var rule = await db.MerchantRules.SingleAsync();
            Assert.Equal("BOOKWORM_ROSEBANK", rule.NormalizedKey);
            Assert.Equal(entertainmentId, rule.CategoryId);
        }

        var rules = new MerchantRuleService(data.Factory);
        var followUp = await rules.GetFollowUpAsync(edited.Id);
        Assert.Equal(new MerchantFollowUp(edited.Description, "Entertainment", 2), followUp);

        Assert.Equal(2, await rules.ApplyToMerchantAsync(edited.Id));
        Assert.Equal(0, (await rules.GetFollowUpAsync(edited.Id))!.OtherTransactions);
        Assert.All(await transactions.GetAllAsync(), t => Assert.Equal(entertainmentId, t.CategoryId));
    }

    [Fact]
    public async Task Short_Or_Disagreeing_First_Words_Are_Not_Generalised()
    {
        await using var data = await TestData.CreateAsync();
        await using (var db = data.NewContext())
        {
            var dining = await data.CategoryIdAsync("Dining");
            var shopping = await data.CategoryIdAsync("Shopping");
            await MerchantRuleBook.RememberAsync(db, "ABC_TRADERS", "ABC Traders", dining, default);
            await MerchantRuleBook.RememberAsync(db, "COFFEEHOUSE_ONE", "Coffeehouse One", dining, default);
            await MerchantRuleBook.RememberAsync(db, "COFFEEHOUSE_TWO", "Coffeehouse Two", shopping, default);
            await db.SaveChangesAsync();
        }

        await using var read = data.NewContext();
        var book = await MerchantRuleBook.LoadAsync(read, default);

        Assert.Equal("Dining", book.CategoryFor("ABC_TRADERS"));
        Assert.Null(book.CategoryFor("ABC_MOTORS"));          // "ABC" is too short to stand for a brand
        Assert.Null(book.CategoryFor("COFFEEHOUSE_THREE"));   // the branches disagree
        Assert.Null(book.CategoryFor("STEAM"));               // known merchants only follow their own rule
    }

    private static void MoveGroup(GroupedMerchantCandidate group, string category)
    {
        group.CategoryName = category;
        foreach (var item in group.Items)
        {
            item.CategoryName = category;
        }
    }

    private static MemoryStream Csv(params (string Date, string Description, decimal Amount)[] rows)
    {
        var balance = 5000m;
        var text = new StringBuilder("Date,Description,Amount,Balance\n");
        foreach (var (date, description, amount) in rows)
        {
            balance += amount;
            text.Append($"{date},{description},{amount:0.00},{balance:0.00}\n");
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private sealed class TestData : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<CoinStackDbContext> _options;

        private TestData(SqliteConnection connection, DbContextOptions<CoinStackDbContext> options)
        {
            _connection = connection;
            _options = options;
            Factory = new TestDbContextFactory(options);
        }

        public TestDbContextFactory Factory { get; }

        public static async Task<TestData> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<CoinStackDbContext>().UseSqlite(connection).Options;

            await using var db = new CoinStackDbContext(options);
            await db.Database.EnsureCreatedAsync();
            await DatabaseInitializer.SeedMissingDefaultCategoriesAsync(db, default);
            return new TestData(connection, options);
        }

        public BankStatementImportService ImportService()
            => new(Factory, new CompositeBankFeed([new FnbPdfBankFeed(), new FnbHistoryBankFeed()]));

        public CoinStackDbContext NewContext() => new(_options);

        public async Task<int> CategoryIdAsync(string name)
        {
            await using var db = NewContext();
            return (await db.Categories.SingleAsync(c => c.Name == name)).Id;
        }

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }
}
