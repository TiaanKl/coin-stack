using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoinStack.Tests;

/// <summary>Backups move everything between devices, and nothing is touched when they can't be opened.</summary>
public sealed class BackupServiceTests : IDisposable
{
    private const string Password = "correct horse battery";

    // Few PBKDF2 rounds keep the tests quick; real backups use 600 000.
    private const int TestIterations = 1_000;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"coinstack-backup-tests-{Guid.NewGuid():N}");

    public BackupServiceTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task A_Backup_Restores_Everything_On_Another_Device()
    {
        var laptop = await DeviceAsync("laptop");
        await using (var db = laptop.Context())
        {
            db.AppSettings.Add(new AppSettings { Currency = "ZAR", MonthStartDay = 25, MonthlyIncome = 30000m });
            db.Transactions.AddRange(
                new Transaction { Description = "Salary", Amount = 30000m, Type = TransactionType.Income, OccurredAtUtc = new DateTime(2026, 9, 25) },
                new Transaction { Description = "Checkers", Amount = 850m, Type = TransactionType.Expense, OccurredAtUtc = new DateTime(2026, 10, 3) });
            await db.SaveChangesAsync();
        }

        var backup = await laptop.Backups().CreateAsync(Password);

        var phone = await DeviceAsync("phone");
        var manifest = await phone.Backups().ReadAsync(backup, Password);
        Assert.Equal(2, manifest.Transactions);
        Assert.Equal(new DateTime(2026, 9, 25), manifest.FirstTransactionUtc);
        Assert.Equal(new DateTime(2026, 10, 3), manifest.LastTransactionUtc);

        var safetyCopy = await phone.Backups().RestoreAsync(backup, Password);

        await using var check = phone.Context();
        Assert.Equal(["Checkers", "Salary"], (await check.Transactions.Select(t => t.Description).ToListAsync()).Order());
        Assert.Equal(25, (await check.AppSettings.SingleAsync()).MonthStartDay);
        Assert.True(File.Exists(safetyCopy), "the phone's own data is saved before it's replaced");
    }

    [Fact]
    public async Task A_Wrong_Password_Or_A_Changed_Byte_Leaves_Everything_As_It_Was()
    {
        var laptop = await DeviceAsync("laptop");
        await AddTransactionAsync(laptop, "From the laptop");
        var backup = await laptop.Backups().CreateAsync(Password);

        var phone = await DeviceAsync("phone");
        await AddTransactionAsync(phone, "Already on the phone");

        var wrong = await Assert.ThrowsAsync<BackupException>(() => phone.Backups().RestoreAsync(backup, "not the password"));
        Assert.Contains("password", wrong.Message);

        var tampered = backup.ToArray();
        tampered[tampered.Length / 2] ^= 0x01;
        await Assert.ThrowsAsync<BackupException>(() => phone.Backups().RestoreAsync(tampered, Password));

        var notABackup = await Assert.ThrowsAsync<BackupException>(() => phone.Backups().ReadAsync("hello"u8.ToArray(), Password));
        Assert.Equal("This isn't a CoinStack backup file.", notABackup.Message);

        await using var check = phone.Context();
        Assert.Equal("Already on the phone", (await check.Transactions.SingleAsync()).Description);
    }

    [Fact]
    public async Task A_Short_Password_Is_Refused()
    {
        var laptop = await DeviceAsync("laptop");

        var error = await Assert.ThrowsAsync<BackupException>(() => laptop.Backups().CreateAsync("short"));
        Assert.Contains("8 characters", error.Message);
    }

    [Fact]
    public async Task A_Backup_From_A_Newer_App_Is_Refused()
    {
        var phone = await DeviceAsync("phone");
        var future = new BackupManifest(1, DateTime.UtcNow, "29991231000000_SomethingNew", 0, null, null);
        var backup = BackupFile.Write(future, [], Password, TestIterations);

        var error = await Assert.ThrowsAsync<BackupException>(() => phone.Backups().RestoreAsync(backup, Password));
        Assert.Contains("newer version", error.Message);
    }

    [Fact]
    public void Each_Backup_Is_Encrypted_Differently()
    {
        var manifest = new BackupManifest(1, DateTime.UtcNow, "", 0, null, null);
        var first = BackupFile.Write(manifest, [1, 2, 3], Password, TestIterations);
        var second = BackupFile.Write(manifest, [1, 2, 3], Password, TestIterations);

        Assert.NotEqual(first, second); // fresh salt and IV each time
        Assert.Equal([1, 2, 3], BackupFile.Read(second, Password).Database);
    }

    private async Task<Device> DeviceAsync(string name)
    {
        var options = new DbContextOptionsBuilder<CoinStackDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_folder, name + ".db")}")
            .Options;

        await using (var db = new CoinStackDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        return new Device(options);
    }

    private static async Task AddTransactionAsync(Device device, string description)
    {
        await using var db = device.Context();
        db.Transactions.Add(new Transaction { Description = description, Amount = 10m, Type = TransactionType.Expense });
        await db.SaveChangesAsync();
    }

    private sealed record Device(DbContextOptions<CoinStackDbContext> Options)
    {
        public CoinStackDbContext Context() => new(Options);

        public BackupService Backups() => new(new TestDbContextFactory(Options), null, TestIterations);
    }
}
