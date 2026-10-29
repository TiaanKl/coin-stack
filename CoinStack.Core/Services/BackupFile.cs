using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CoinStack.Services;

/// <summary>What a backup says about itself, readable only with its password.</summary>
public sealed record BackupManifest(
    int Format,
    DateTime CreatedUtc,
    string SchemaVersion,
    int Transactions,
    DateTime? FirstTransactionUtc,
    DateTime? LastTransactionUtc);

/// <summary>A wrong password, a damaged file, or a backup this version of the app can't restore.</summary>
public sealed class BackupException(string message) : Exception(message);

/// <summary>
/// The <c>.coinstack</c> file: the database and a manifest, zipped, then encrypted with the person's password.
/// AES-256-CBC with an HMAC-SHA256 over everything (encrypt-then-MAC), both keys from PBKDF2-SHA256, so it
/// works on every platform the apps run on. A wrong password or any changed byte fails the HMAC check before
/// anything is decrypted.
/// <code>
/// "CSBACKUP" | version (1 byte) | PBKDF2 rounds (int32) | salt (16) | IV (16) | ciphertext | HMAC (32)
/// </code>
/// </summary>
internal static class BackupFile
{
    public const int DefaultIterations = 600_000;
    public const int MinimumPasswordLength = 8;

    private const int CurrentVersion = 1;
    private const int SaltSize = 16;
    private const int IvSize = 16;
    private const int KeySize = 32;
    private const int TagSize = 32;
    private const int MinimumIterations = 1_000;
    private const int MaximumIterations = 10_000_000;
    private const string ManifestEntry = "manifest.json";
    private const string DatabaseEntry = "coinstack.db";

    private static readonly byte[] Magic = "CSBACKUP"u8.ToArray();
    private static readonly int HeaderSize = Magic.Length + 1 + sizeof(int) + SaltSize + IvSize;

    private const string WrongPasswordMessage = "That password doesn't open this backup, or the file is damaged.";

    public static byte[] Write(BackupManifest manifest, byte[] database, string password, int iterations = DefaultIterations)
    {
        var plain = Zip(manifest, database);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var iv = RandomNumberGenerator.GetBytes(IvSize);
        var (encryptionKey, macKey) = DeriveKeys(password, salt, iterations);

        using var aes = Aes.Create();
        aes.Key = encryptionKey;
        var ciphertext = aes.EncryptCbc(plain, iv, PaddingMode.PKCS7);

        var file = new byte[HeaderSize + ciphertext.Length + TagSize];
        var span = file.AsSpan();
        Magic.CopyTo(span);
        span[Magic.Length] = CurrentVersion;
        BinaryPrimitives.WriteInt32LittleEndian(span[(Magic.Length + 1)..], iterations);
        salt.CopyTo(span[(Magic.Length + 1 + sizeof(int))..]);
        iv.CopyTo(span[(HeaderSize - IvSize)..]);
        ciphertext.CopyTo(span[HeaderSize..]);
        HMACSHA256.HashData(macKey, span[..^TagSize]).CopyTo(span[^TagSize..]);

        return file;
    }

    public static (BackupManifest Manifest, byte[] Database) Read(byte[] file, string password)
    {
        var span = file.AsSpan();
        if (file.Length < HeaderSize + IvSize + TagSize || !span[..Magic.Length].SequenceEqual(Magic))
        {
            throw new BackupException("This isn't a CoinStack backup file.");
        }

        if (span[Magic.Length] != CurrentVersion)
        {
            throw new BackupException("This backup was made by a newer version of CoinStack. Update the app, then restore it.");
        }

        var iterations = BinaryPrimitives.ReadInt32LittleEndian(span[(Magic.Length + 1)..]);
        if (iterations is < MinimumIterations or > MaximumIterations)
        {
            throw new BackupException(WrongPasswordMessage);
        }

        var salt = span.Slice(Magic.Length + 1 + sizeof(int), SaltSize).ToArray();
        var iv = span.Slice(HeaderSize - IvSize, IvSize).ToArray();
        var (encryptionKey, macKey) = DeriveKeys(password, salt, iterations);

        var expectedTag = HMACSHA256.HashData(macKey, span[..^TagSize]);
        if (!CryptographicOperations.FixedTimeEquals(expectedTag, span[^TagSize..]))
        {
            throw new BackupException(WrongPasswordMessage);
        }

        byte[] plain;
        try
        {
            using var aes = Aes.Create();
            aes.Key = encryptionKey;
            plain = aes.DecryptCbc(span[HeaderSize..^TagSize], iv, PaddingMode.PKCS7);
        }
        catch (CryptographicException)
        {
            throw new BackupException(WrongPasswordMessage);
        }

        return Unzip(plain);
    }

    private static (byte[] EncryptionKey, byte[] MacKey) DeriveKeys(string password, byte[] salt, int iterations)
    {
        var keys = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, KeySize * 2);
        return (keys[..KeySize], keys[KeySize..]);
    }

    private static byte[] Zip(BackupManifest manifest, byte[] database)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var entry = zip.CreateEntry(ManifestEntry, CompressionLevel.Optimal).Open())
            {
                JsonSerializer.Serialize(entry, manifest);
            }

            using (var entry = zip.CreateEntry(DatabaseEntry, CompressionLevel.Optimal).Open())
            {
                entry.Write(database);
            }
        }

        return buffer.ToArray();
    }

    private static (BackupManifest Manifest, byte[] Database) Unzip(byte[] plain)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(plain), ZipArchiveMode.Read);
            using var manifestStream = zip.GetEntry(ManifestEntry)?.Open() ?? throw new InvalidDataException();
            var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestStream) ?? throw new InvalidDataException();

            using var databaseStream = zip.GetEntry(DatabaseEntry)?.Open() ?? throw new InvalidDataException();
            using var database = new MemoryStream();
            databaseStream.CopyTo(database);
            return (manifest, database.ToArray());
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException)
        {
            throw new BackupException("This backup file is damaged.");
        }
    }
}
