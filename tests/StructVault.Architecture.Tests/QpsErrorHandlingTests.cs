using System.Data.Common;
using System.IO;
using Microsoft.Data.Sqlite;
using StructVault.Application.Abstractions.Persistence;
using StructVault.Application.Abstractions.Security;
using StructVault.Application.Abstractions.Storage;
using StructVault.Application.Errors;
using StructVault.Application.Qps;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class QpsErrorHandlingTests
{
    [Fact]
    public async Task TrySaveReturnsValidationFailureWithoutSerializingWhenPasswordIsBlank()
    {
        RecordingVaultDatabaseSerializer serializer = new();
        TrySaveQpsVaultFileCommandHandler handler = CreateHandler(serializer, new RecordingQpsFileWriter());
        await using SqliteConnection connection = await OpenConnectionAsync();

        VaultOperationResult result = await handler.Handle(
            new TrySaveQpsVaultFileCommand(connection, "vault.qps", " "),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VaultOperationErrorCode.ValidationFailed, result.Error?.Code);
        Assert.False(serializer.SerializeWasCalled);
        Assert.DoesNotContain("vault.qps", result.Error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TrySaveReturnsFileAccessFailureWhenWriterCannotWriteVaultFile()
    {
        RecordingVaultDatabaseSerializer serializer = new();
        RecordingQpsFileWriter writer = new() { ExceptionToThrow = new IOException("/secret/path/vault.qps is locked") };
        TrySaveQpsVaultFileCommandHandler handler = CreateHandler(serializer, writer);
        await using SqliteConnection connection = await OpenConnectionAsync();

        VaultOperationResult result = await handler.Handle(
            new TrySaveQpsVaultFileCommand(connection, "vault.qps", "strong-password"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VaultOperationErrorCode.FileAccessFailed, result.Error?.Code);
        Assert.True(serializer.SerializeWasCalled);
        Assert.True(writer.WriteWasCalled);
        Assert.DoesNotContain("/secret/path", result.Error?.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("strong-password", result.Error?.Message, StringComparison.Ordinal);
    }

    private static TrySaveQpsVaultFileCommandHandler CreateHandler(
        IVaultDatabaseSerializer serializer,
        IQpsFileWriter writer)
    {
        return new TrySaveQpsVaultFileCommandHandler(
            serializer,
            new RecordingKeyDerivationService(),
            new RecordingEncryptionService(),
            new RecordingQpsFileBackupService(),
            writer);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
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
            throw new NotSupportedException("QPS error handling tests do not deserialize databases.");
        }
    }

    private sealed class RecordingKeyDerivationService : IKeyDerivationService
    {
        public int KeySizeInBytes => 32;

        public byte[] DeriveKey(string password, ReadOnlyMemory<byte> salt)
        {
            return Enumerable.Repeat((byte)7, KeySizeInBytes).ToArray();
        }
    }

    private sealed class RecordingEncryptionService : IAuthenticatedEncryptionService
    {
        public int KeySizeInBytes => 32;

        public int NonceSizeInBytes => 12;

        public int TagSizeInBytes => 16;

        public AesGcmEncryptionResult Encrypt(ReadOnlyMemory<byte> plaintext, ReadOnlyMemory<byte> key)
        {
            return new AesGcmEncryptionResult(
                Enumerable.Repeat((byte)1, NonceSizeInBytes).ToArray(),
                plaintext.ToArray(),
                Enumerable.Repeat((byte)2, TagSizeInBytes).ToArray());
        }

        public byte[] Decrypt(
            ReadOnlyMemory<byte> ciphertext,
            ReadOnlyMemory<byte> key,
            ReadOnlyMemory<byte> nonce,
            ReadOnlyMemory<byte> tag)
        {
            throw new NotSupportedException("QPS error handling tests do not decrypt vault data.");
        }
    }

    private sealed class RecordingQpsFileBackupService : IQpsFileBackupService
    {
        public Task BackupAsync(string filePath, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task RestoreAsync(string filePath, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingQpsFileWriter : IQpsFileWriter
    {
        public bool WriteWasCalled { get; private set; }

        public Exception? ExceptionToThrow { get; init; }

        public Task WriteAsync(string filePath, ReadOnlyMemory<byte> fileBytes, CancellationToken cancellationToken)
        {
            WriteWasCalled = true;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.CompletedTask;
        }
    }
}
