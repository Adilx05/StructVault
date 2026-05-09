using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using StructVault.Application.Abstractions.Storage;
using StructVault.Application.Qps;
using StructVault.Infrastructure.Security;
using StructVault.Infrastructure.Storage;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class QpsChangeMasterPasswordTests
{
    private const string CurrentPassword = "current-master-password";
    private const string NewPassword = "new-master-password";

    [Fact]
    public async Task ChangeMasterPasswordRejectsOldPasswordAndAllowsNewPasswordWithoutChangingVaultData()
    {
        string directoryPath = CreateUniqueTempDirectoryPath();
        string vaultFilePath = Path.Combine(directoryPath, "vault.qps");
        string backupFilePath = FileSystemQpsFileBackupService.CreateBackupPath(vaultFilePath);
        byte[] sensitiveFieldValue = Encoding.UTF8.GetBytes("change-master-password-secret");
        SqliteVaultSchemaProvider schemaProvider = new();
        SqliteInMemoryVaultDatabaseConnectionFactory connectionFactory = new(schemaProvider);
        SqliteVaultDatabaseSerializer serializer = new(schemaProvider);
        SaveQpsVaultFileCommandHandler saveHandler = new(
            serializer,
            new Argon2idKeyDerivationService(),
            new Aes256GcmEncryptionService(),
            new FileSystemQpsFileBackupService(),
            new FileSystemQpsFileWriter());
        ChangeQpsVaultMasterPasswordCommandHandler changePasswordHandler = CreateHandler();
        OpenQpsVaultFileQueryHandler openHandler = CreateOpenHandler();

        try
        {
            await using DbConnection sourceConnection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
            await InsertVaultDataAsync(sourceConnection, sensitiveFieldValue);
            await saveHandler.Handle(new SaveQpsVaultFileCommand(sourceConnection, vaultFilePath, CurrentPassword), CancellationToken.None);
            byte[] originalQpsFileBytes = await File.ReadAllBytesAsync(vaultFilePath);

            await changePasswordHandler.Handle(
                new ChangeQpsVaultMasterPasswordCommand(vaultFilePath, CurrentPassword, NewPassword),
                CancellationToken.None);

            Assert.True(File.Exists(backupFilePath));
            Assert.Equal(originalQpsFileBytes, await File.ReadAllBytesAsync(backupFilePath));
            await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
                await openHandler.Handle(new OpenQpsVaultFileQuery(vaultFilePath, CurrentPassword), CancellationToken.None));

            byte[] changedQpsFileBytes = await File.ReadAllBytesAsync(vaultFilePath);
            Assert.NotEqual(originalQpsFileBytes, changedQpsFileBytes);
            Assert.False(ContainsSequence(changedQpsFileBytes, sensitiveFieldValue));

            byte[] databaseImage = await openHandler.Handle(new OpenQpsVaultFileQuery(vaultFilePath, NewPassword), CancellationToken.None);
            await using DbConnection restoredConnection = await serializer.DeserializeAsync(databaseImage, CancellationToken.None);

            Assert.Equal("Root", await ExecuteScalarAsync(restoredConnection, "SELECT Name FROM VaultNode WHERE Id = 'root';"));
            Assert.Equal(sensitiveFieldValue, await ExecuteBytesAsync(restoredConnection, "SELECT Value FROM VaultField WHERE Id = 'field';"));

            ZeroMemory(originalQpsFileBytes);
            ZeroMemory(changedQpsFileBytes);
            ZeroMemory(databaseImage);
        }
        finally
        {
            ZeroMemory(sensitiveFieldValue);
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact]
    public async Task ChangeMasterPasswordRejectsInvalidCurrentPasswordBeforeBackupOrWrite()
    {
        string directoryPath = CreateUniqueTempDirectoryPath();
        string vaultFilePath = Path.Combine(directoryPath, "vault.qps");
        byte[] sensitiveFieldValue = Encoding.UTF8.GetBytes("unchanged-password-secret");
        SqliteVaultSchemaProvider schemaProvider = new();
        SqliteInMemoryVaultDatabaseConnectionFactory connectionFactory = new(schemaProvider);
        SqliteVaultDatabaseSerializer serializer = new(schemaProvider);
        SaveQpsVaultFileCommandHandler saveHandler = new(
            serializer,
            new Argon2idKeyDerivationService(),
            new Aes256GcmEncryptionService(),
            new FileSystemQpsFileBackupService(),
            new FileSystemQpsFileWriter());
        RecordingQpsFileBackupService backupService = new();
        RecordingQpsFileWriter writer = new();
        ChangeQpsVaultMasterPasswordCommandHandler changePasswordHandler = new(
            new FileSystemQpsFileReader(),
            new Argon2idKeyDerivationService(),
            new Aes256GcmEncryptionService(),
            backupService,
            writer);

        try
        {
            await using DbConnection sourceConnection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
            await InsertVaultDataAsync(sourceConnection, sensitiveFieldValue);
            await saveHandler.Handle(new SaveQpsVaultFileCommand(sourceConnection, vaultFilePath, CurrentPassword), CancellationToken.None);
            byte[] originalQpsFileBytes = await File.ReadAllBytesAsync(vaultFilePath);

            await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
                await changePasswordHandler.Handle(
                    new ChangeQpsVaultMasterPasswordCommand(vaultFilePath, "wrong-current-password", NewPassword),
                    CancellationToken.None));

            Assert.False(backupService.BackupWasCalled);
            Assert.False(writer.WriteWasCalled);
            Assert.Equal(originalQpsFileBytes, await File.ReadAllBytesAsync(vaultFilePath));

            ZeroMemory(originalQpsFileBytes);
        }
        finally
        {
            ZeroMemory(sensitiveFieldValue);
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Theory]
    [InlineData("", CurrentPassword, NewPassword)]
    [InlineData("vault.qps", "", NewPassword)]
    [InlineData("vault.qps", CurrentPassword, "")]
    [InlineData("vault.qps", CurrentPassword, CurrentPassword)]
    public async Task ChangeMasterPasswordValidatesInputsBeforeReadingBackupOrWrite(
        string filePath,
        string currentPassword,
        string newPassword)
    {
        RecordingQpsFileReader reader = new();
        RecordingQpsFileBackupService backupService = new();
        RecordingQpsFileWriter writer = new();
        ChangeQpsVaultMasterPasswordCommandHandler handler = new(
            reader,
            new Argon2idKeyDerivationService(),
            new Aes256GcmEncryptionService(),
            backupService,
            writer);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(
                new ChangeQpsVaultMasterPasswordCommand(filePath, currentPassword, newPassword),
                CancellationToken.None));

        Assert.False(reader.ReadWasCalled);
        Assert.False(backupService.BackupWasCalled);
        Assert.False(writer.WriteWasCalled);
    }

    private static ChangeQpsVaultMasterPasswordCommandHandler CreateHandler()
    {
        return new ChangeQpsVaultMasterPasswordCommandHandler(
            new FileSystemQpsFileReader(),
            new Argon2idKeyDerivationService(),
            new Aes256GcmEncryptionService(),
            new FileSystemQpsFileBackupService(),
            new FileSystemQpsFileWriter());
    }

    private static OpenQpsVaultFileQueryHandler CreateOpenHandler()
    {
        return new OpenQpsVaultFileQueryHandler(
            new FileSystemQpsFileReader(),
            new Argon2idKeyDerivationService(),
            new Aes256GcmEncryptionService());
    }

    private static async Task InsertVaultDataAsync(DbConnection connection, byte[] fieldValue)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO VaultNode (Id, ParentNodeId, Name, SortOrder, CreatedAtUtc, UpdatedAtUtc)
            VALUES ('root', NULL, 'Root', 0, '2026-05-09T00:00:00Z', '2026-05-09T00:00:00Z');

            INSERT INTO VaultField (Id, NodeId, Key, Value, SortOrder, CreatedAtUtc, UpdatedAtUtc)
            VALUES ('field', 'root', 'Secret', $value, 0, '2026-05-09T00:00:00Z', '2026-05-09T00:00:00Z');
            """;
        DbParameter valueParameter = command.CreateParameter();
        valueParameter.ParameterName = "$value";
        valueParameter.Value = fieldValue;
        command.Parameters.Add(valueParameter);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ExecuteScalarAsync(DbConnection connection, string commandText)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        return await command.ExecuteScalarAsync();
    }

    private static async Task<byte[]> ExecuteBytesAsync(DbConnection connection, string commandText)
    {
        object? result = await ExecuteScalarAsync(connection, commandText);
        return Assert.IsType<byte[]>(result);
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
        public bool ReadWasCalled { get; private set; }

        public Task<byte[]> ReadAsync(string filePath, CancellationToken cancellationToken)
        {
            ReadWasCalled = true;
            return Task.FromResult(Array.Empty<byte>());
        }
    }

    private sealed class RecordingQpsFileBackupService : IQpsFileBackupService
    {
        public bool BackupWasCalled { get; private set; }

        public Task BackupAsync(string filePath, CancellationToken cancellationToken)
        {
            BackupWasCalled = true;
            return Task.CompletedTask;
        }

        public Task RestoreAsync(string filePath, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Change password tests do not restore backups.");
        }
    }

    private sealed class RecordingQpsFileWriter : IQpsFileWriter
    {
        public bool WriteWasCalled { get; private set; }

        public Task WriteAsync(string filePath, ReadOnlyMemory<byte> fileBytes, CancellationToken cancellationToken)
        {
            WriteWasCalled = true;
            return Task.CompletedTask;
        }
    }
}
