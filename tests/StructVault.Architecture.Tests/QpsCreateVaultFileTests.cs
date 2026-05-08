using System.Security.Cryptography;
using System.Text;
using StructVault.Application.Abstractions.Security;
using StructVault.Application.Qps;
using StructVault.Application.Security;
using StructVault.Infrastructure.Security;
using StructVault.Infrastructure.Storage;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class QpsCreateVaultFileTests
{
    private static readonly byte[] ValidSalt =
    [
        0x24, 0x78, 0x4B, 0xC1,
        0x52, 0x0E, 0x9F, 0xA4,
        0xB6, 0x33, 0x71, 0xDD,
        0x8A, 0x19, 0xE5, 0x40,
    ];

    [Fact]
    public async Task CreateVaultWritesGeneratedQpsFileToDisk()
    {
        string directoryPath = CreateUniqueTempDirectoryPath();
        string vaultFilePath = Path.Combine(directoryPath, "nested", "vault.qps");
        byte[] plaintextVaultData = Encoding.UTF8.GetBytes("initial encrypted vault payload");
        byte[] key = await DeriveVaultKey();
        AesGcmEncryptionResult encryptionResult = await EncryptVaultData(plaintextVaultData, key);
        byte[] qpsFileBytes = await CreateQpsVaultFile(encryptionResult);
        WriteQpsVaultFileCommandHandler writeHandler = new(new FileSystemQpsFileWriter());
        ParseQpsVaultFileQueryHandler parseHandler = new();

        try
        {
            Assert.False(File.Exists(vaultFilePath));

            await writeHandler.Handle(new WriteQpsVaultFileCommand(vaultFilePath, qpsFileBytes), CancellationToken.None);

            Assert.True(File.Exists(vaultFilePath));
            byte[] generatedFileBytes = await File.ReadAllBytesAsync(vaultFilePath);
            Assert.Equal(qpsFileBytes, generatedFileBytes);

            QpsVaultFile generatedVaultFile = await parseHandler.Handle(
                new ParseQpsVaultFileQuery(generatedFileBytes),
                CancellationToken.None);

            Assert.Equal(QpsFileFormat.CurrentVersion, generatedVaultFile.Version);
            Assert.Equal(ValidSalt, generatedVaultFile.Salt.ToArray());
            Assert.Equal(encryptionResult.Nonce.ToArray(), generatedVaultFile.InitializationVector.ToArray());
            Assert.Equal(encryptionResult.Ciphertext.ToArray(), generatedVaultFile.Ciphertext.ToArray());
            Assert.Equal(encryptionResult.Tag.ToArray(), generatedVaultFile.AuthenticationTag.ToArray());
        }
        finally
        {
            ZeroMemory(key);
            ZeroMemory(plaintextVaultData);
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task<byte[]> DeriveVaultKey()
    {
        DeriveVaultKeyCommandHandler handler = new(new Argon2idKeyDerivationService());
        return await handler.Handle(
            new DeriveVaultKeyCommand("correct horse battery staple", ValidSalt),
            CancellationToken.None);
    }

    private static async Task<AesGcmEncryptionResult> EncryptVaultData(byte[] plaintextVaultData, byte[] key)
    {
        EncryptVaultDataCommandHandler handler = new(new Aes256GcmEncryptionService());
        return await handler.Handle(
            new EncryptVaultDataCommand(plaintextVaultData, key),
            CancellationToken.None);
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
}
