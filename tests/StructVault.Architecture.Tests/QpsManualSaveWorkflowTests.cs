using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using StructVault.Application.Abstractions.Persistence;
using StructVault.Application.Abstractions.Storage;
using StructVault.Application.Qps;
using StructVault.Infrastructure.Security;
using StructVault.Infrastructure.Storage;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class QpsManualSaveWorkflowTests
{
    private const string VaultPassword = "manual-save-password";

    [Fact]
    public async Task ManualSaveWritesEncryptedQpsFileThatRestoresVaultDatabase()
    {
        string directoryPath = CreateUniqueTempDirectoryPath();
        string vaultFilePath = Path.Combine(directoryPath, "vault.qps");
        byte[] sensitiveFieldValue = Encoding.UTF8.GetBytes("manual-save-secret-value");
        SqliteVaultSchemaProvider schemaProvider = new();
        SqliteInMemoryVaultDatabaseConnectionFactory connectionFactory = new(schemaProvider);
        SqliteVaultDatabaseSerializer serializer = new(schemaProvider);
        SaveQpsVaultFileCommandHandler saveHandler = new(
            serializer,
            new Argon2idKeyDerivationService(),
            new Aes256GcmEncryptionService(),
            new FileSystemQpsFileWriter());
        OpenQpsVaultFileQueryHandler openHandler = new(
            new FileSystemQpsFileReader(),
            new Argon2idKeyDerivationService(),
            new Aes256GcmEncryptionService());

        try
        {
            await using DbConnection sourceConnection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
            await InsertVaultDataAsync(sourceConnection, sensitiveFieldValue);

            await saveHandler.Handle(new SaveQpsVaultFileCommand(sourceConnection, vaultFilePath, VaultPassword), CancellationToken.None);

            Assert.True(File.Exists(vaultFilePath));
            byte[] qpsFileBytes = await File.ReadAllBytesAsync(vaultFilePath);
            Assert.False(ContainsSequence(qpsFileBytes, sensitiveFieldValue));

            byte[] databaseImage = await openHandler.Handle(new OpenQpsVaultFileQuery(vaultFilePath, VaultPassword), CancellationToken.None);
            await using DbConnection restoredConnection = await serializer.DeserializeAsync(databaseImage, CancellationToken.None);

            Assert.Equal("Root", await ExecuteScalarAsync(restoredConnection, "SELECT Name FROM VaultNode WHERE Id = 'root';"));
            Assert.Equal(sensitiveFieldValue, await ExecuteBytesAsync(restoredConnection, "SELECT Value FROM VaultField WHERE Id = 'field';"));

            ZeroMemory(databaseImage);
            ZeroMemory(qpsFileBytes);
        }
        finally
        {
            ZeroMemory(sensitiveFieldValue);
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact]
    public async Task ManualSaveRejectsBlankPasswordBeforeSerializingOrWriting()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory connectionFactory = new(new SqliteVaultSchemaProvider());
        RecordingVaultDatabaseSerializer serializer = new();
        RecordingQpsFileWriter writer = new();
        SaveQpsVaultFileCommandHandler handler = new(
            serializer,
            new Argon2idKeyDerivationService(),
            new Aes256GcmEncryptionService(),
            writer);

        await using DbConnection connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new SaveQpsVaultFileCommand(connection, "vault.qps", "   "), CancellationToken.None));

        Assert.False(serializer.SerializeWasCalled);
        Assert.False(writer.WriteWasCalled);
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

    private sealed class RecordingVaultDatabaseSerializer : IVaultDatabaseSerializer
    {
        public bool SerializeWasCalled { get; private set; }

        public Task<byte[]> SerializeAsync(DbConnection connection, CancellationToken cancellationToken)
        {
            SerializeWasCalled = true;
            return Task.FromResult(new byte[] { 1, 2, 3 });
        }

        public Task<DbConnection> DeserializeAsync(byte[] databaseImage, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Manual save validation tests do not deserialize databases.");
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
