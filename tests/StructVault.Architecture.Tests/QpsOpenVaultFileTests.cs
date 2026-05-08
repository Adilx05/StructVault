using System.Security.Cryptography;
using System.Text;
using StructVault.Application.Abstractions.Security;
using StructVault.Application.Abstractions.Storage;
using StructVault.Application.Qps;
using StructVault.Application.Security;
using StructVault.Infrastructure.Security;
using StructVault.Infrastructure.Storage;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class QpsOpenVaultFileTests
{
    private const string VaultPassword = "correct horse battery staple";

    private static readonly byte[] ValidSalt =
    [
        0x6A, 0x91, 0xD2, 0x48,
        0x03, 0x7C, 0xEF, 0xB5,
        0x2D, 0x84, 0x10, 0xA6,
        0x99, 0x3F, 0xC1, 0x5E,
    ];

    [Fact]
    public async Task OpenVaultFileLoadsOriginalEncryptedPayloadFromDisk()
    {
        string directoryPath = CreateUniqueTempDirectoryPath();
        string vaultFilePath = Path.Combine(directoryPath, "nested", "vault.qps");
        byte[] plaintextVaultData = Encoding.UTF8.GetBytes(
            "StructVault QPS open test payload v1\nnode:Root\nfield:url=https://example.invalid");
        byte[] key = await DeriveVaultKey(VaultPassword, ValidSalt);
        AesGcmEncryptionResult encryptionResult = await EncryptVaultData(plaintextVaultData, key);
        byte[] qpsFileBytes = await CreateQpsVaultFile(encryptionResult);
        WriteQpsVaultFileCommandHandler writeHandler = new(new FileSystemQpsFileWriter());
        OpenQpsVaultFileQueryHandler openHandler = new(
            new FileSystemQpsFileReader(),
            new Argon2idKeyDerivationService(),
            new Aes256GcmEncryptionService());

        try
        {
            Assert.False(ContainsSequence(qpsFileBytes, plaintextVaultData));

            await writeHandler.Handle(new WriteQpsVaultFileCommand(vaultFilePath, qpsFileBytes), CancellationToken.None);

            byte[] persistedFileBytes = await File.ReadAllBytesAsync(vaultFilePath, CancellationToken.None);
            Assert.False(ContainsSequence(persistedFileBytes, plaintextVaultData));

            byte[] loadedVaultData = await openHandler.Handle(
                new OpenQpsVaultFileQuery(vaultFilePath, VaultPassword),
                CancellationToken.None);

            try
            {
                Assert.Equal(plaintextVaultData, loadedVaultData);
            }
            finally
            {
                ZeroMemory(loadedVaultData);
            }
        }
        finally
        {
            ZeroMemory(key);
            ZeroMemory(plaintextVaultData);
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact]
    public async Task OpenVaultFileFailsWhenPasswordIsInvalid()
    {
        const string invalidPassword = "incorrect horse battery staple";
        string directoryPath = CreateUniqueTempDirectoryPath();
        string vaultFilePath = Path.Combine(directoryPath, "vault.qps");
        byte[] plaintextVaultData = Encoding.UTF8.GetBytes(
            "StructVault QPS invalid password test payload v1\nnode:Root\nfield:secret=encrypted-only");
        byte[] key = await DeriveVaultKey(VaultPassword, ValidSalt);
        AesGcmEncryptionResult encryptionResult = await EncryptVaultData(plaintextVaultData, key);
        byte[] qpsFileBytes = await CreateQpsVaultFile(encryptionResult);
        WriteQpsVaultFileCommandHandler writeHandler = new(new FileSystemQpsFileWriter());
        OpenQpsVaultFileQueryHandler openHandler = new(
            new FileSystemQpsFileReader(),
            new Argon2idKeyDerivationService(),
            new Aes256GcmEncryptionService());

        try
        {
            await writeHandler.Handle(new WriteQpsVaultFileCommand(vaultFilePath, qpsFileBytes), CancellationToken.None);

            await Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () =>
                await openHandler.Handle(new OpenQpsVaultFileQuery(vaultFilePath, invalidPassword), CancellationToken.None));
        }
        finally
        {
            ZeroMemory(key);
            ZeroMemory(plaintextVaultData);
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact]
    public async Task OpenVaultFileFailsSafelyWhenEncryptedPayloadIsCorrupted()
    {
        string directoryPath = CreateUniqueTempDirectoryPath();
        string vaultFilePath = Path.Combine(directoryPath, "corrupted-vault.qps");
        byte[] plaintextVaultData = Encoding.UTF8.GetBytes(
            "StructVault QPS corrupted file test payload v1\nnode:Root\nfield:secret=encrypted-only");
        byte[] key = await DeriveVaultKey(VaultPassword, ValidSalt);
        AesGcmEncryptionResult encryptionResult = await EncryptVaultData(plaintextVaultData, key);
        byte[] qpsFileBytes = await CreateQpsVaultFile(encryptionResult);
        WriteQpsVaultFileCommandHandler writeHandler = new(new FileSystemQpsFileWriter());
        OpenQpsVaultFileQueryHandler openHandler = new(
            new FileSystemQpsFileReader(),
            new Argon2idKeyDerivationService(),
            new Aes256GcmEncryptionService());

        try
        {
            CorruptFirstCiphertextByte(qpsFileBytes, encryptionResult.Nonce.Length);
            Assert.False(ContainsSequence(qpsFileBytes, plaintextVaultData));

            await writeHandler.Handle(new WriteQpsVaultFileCommand(vaultFilePath, qpsFileBytes), CancellationToken.None);

            await Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () =>
                await openHandler.Handle(new OpenQpsVaultFileQuery(vaultFilePath, VaultPassword), CancellationToken.None));
        }
        finally
        {
            ZeroMemory(key);
            ZeroMemory(plaintextVaultData);
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact]
    public async Task OpenVaultFileRejectsBlankPathBeforeReading()
    {
        RecordingQpsFileReader reader = new();
        OpenQpsVaultFileQueryHandler handler = CreateOpenHandler(reader);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new OpenQpsVaultFileQuery(" ", VaultPassword), CancellationToken.None));

        Assert.False(reader.WasCalled);
    }

    [Fact]
    public async Task OpenVaultFileRejectsBlankPasswordBeforeReading()
    {
        RecordingQpsFileReader reader = new();
        OpenQpsVaultFileQueryHandler handler = CreateOpenHandler(reader);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new OpenQpsVaultFileQuery("vault.qps", " "), CancellationToken.None));

        Assert.False(reader.WasCalled);
    }

    private static OpenQpsVaultFileQueryHandler CreateOpenHandler(IQpsFileReader reader)
    {
        return new OpenQpsVaultFileQueryHandler(
            reader,
            new Argon2idKeyDerivationService(),
            new Aes256GcmEncryptionService());
    }

    private static async Task<byte[]> DeriveVaultKey(string password, byte[] salt)
    {
        DeriveVaultKeyCommandHandler handler = new(new Argon2idKeyDerivationService());
        return await handler.Handle(new DeriveVaultKeyCommand(password, salt), CancellationToken.None);
    }

    private static async Task<AesGcmEncryptionResult> EncryptVaultData(byte[] plaintextVaultData, byte[] key)
    {
        EncryptVaultDataCommandHandler handler = new(new Aes256GcmEncryptionService());
        return await handler.Handle(new EncryptVaultDataCommand(plaintextVaultData, key), CancellationToken.None);
    }

    private static async Task<byte[]> CreateQpsVaultFile(AesGcmEncryptionResult encryptionResult)
    {
        CreateQpsVaultFileCommandHandler handler = new();
        return await handler.Handle(
            new CreateQpsVaultFileCommand(
                ValidSalt,
                encryptionResult.Nonce.ToArray(),
                encryptionResult.Ciphertext.ToArray(),
                encryptionResult.Tag.ToArray()),
            CancellationToken.None);
    }

    private static void CorruptFirstCiphertextByte(byte[] qpsFileBytes, int initializationVectorLength)
    {
        int ciphertextOffset = QpsFileFormat.HeaderSizeInBytes + ValidSalt.Length + initializationVectorLength;
        qpsFileBytes[ciphertextOffset] ^= 0xFF;
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0)
        {
            return true;
        }

        if (haystack.Length < needle.Length)
        {
            return false;
        }

        for (int index = 0; index <= haystack.Length - needle.Length; index++)
        {
            if (haystack.AsSpan(index, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }

    private static string CreateUniqueTempDirectoryPath()
    {
        return Path.Combine(Path.GetTempPath(), "StructVaultTests", Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private static void ZeroMemory(byte[] bytes)
    {
        if (bytes.Length > 0)
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private sealed class RecordingQpsFileReader : IQpsFileReader
    {
        public bool WasCalled { get; private set; }

        public Task<byte[]> ReadAsync(string filePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WasCalled = true;

            return Task.FromResult(Array.Empty<byte>());
        }
    }
}
