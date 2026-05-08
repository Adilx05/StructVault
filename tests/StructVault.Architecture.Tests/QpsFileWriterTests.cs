using StructVault.Application.Abstractions.Storage;
using StructVault.Application.Qps;
using StructVault.Infrastructure.Storage;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class QpsFileWriterTests
{
    private static readonly byte[] ValidSalt =
    [
        0x10, 0x21, 0x32, 0x43,
        0x54, 0x65, 0x76, 0x87,
        0x98, 0xA9, 0xBA, 0xCB,
        0xDC, 0xED, 0xFE, 0x0F,
    ];

    private static readonly byte[] ValidInitializationVector =
    [
        0xA0, 0xA1, 0xA2, 0xA3,
        0xA4, 0xA5, 0xA6, 0xA7,
        0xA8, 0xA9, 0xAA, 0xAB,
    ];

    private static readonly byte[] ValidCiphertext = [0x01, 0x02, 0x03, 0x04, 0x05];

    private static readonly byte[] ValidAuthenticationTag =
    [
        0xB0, 0xB1, 0xB2, 0xB3,
        0xB4, 0xB5, 0xB6, 0xB7,
        0xB8, 0xB9, 0xBA, 0xBB,
        0xBC, 0xBD, 0xBE, 0xBF,
    ];

    [Fact]
    public async Task HandlerWritesValidQpsBytesThroughWriter()
    {
        byte[] qpsBytes = await CreateValidQpsBytes();
        RecordingQpsFileWriter writer = new();
        WriteQpsVaultFileCommandHandler handler = new(writer);

        await handler.Handle(new WriteQpsVaultFileCommand("vault.qps", qpsBytes), CancellationToken.None);

        Assert.Equal("vault.qps", writer.FilePath);
        Assert.Equal(qpsBytes, writer.FileBytes);
    }

    [Fact]
    public async Task HandlerRejectsBlankPathBeforeWriting()
    {
        byte[] qpsBytes = await CreateValidQpsBytes();
        RecordingQpsFileWriter writer = new();
        WriteQpsVaultFileCommandHandler handler = new(writer);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new WriteQpsVaultFileCommand(" ", qpsBytes), CancellationToken.None));

        Assert.False(writer.WasCalled);
    }

    [Fact]
    public async Task HandlerRejectsInvalidQpsBytesBeforeWriting()
    {
        RecordingQpsFileWriter writer = new();
        WriteQpsVaultFileCommandHandler handler = new(writer);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new WriteQpsVaultFileCommand("vault.qps", [0x01, 0x02]), CancellationToken.None));

        Assert.False(writer.WasCalled);
    }

    [Fact]
    public async Task HandlerHonorsCancellationBeforeWriting()
    {
        byte[] qpsBytes = await CreateValidQpsBytes();
        RecordingQpsFileWriter writer = new();
        WriteQpsVaultFileCommandHandler handler = new(writer);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await handler.Handle(new WriteQpsVaultFileCommand("vault.qps", qpsBytes), cancellation.Token));

        Assert.False(writer.WasCalled);
    }

    [Fact]
    public async Task FileSystemWriterCreatesVaultFileAndParentDirectory()
    {
        byte[] qpsBytes = await CreateValidQpsBytes();
        string directoryPath = CreateUniqueTempDirectoryPath();
        string vaultFilePath = Path.Combine(directoryPath, "nested", "vault.qps");
        FileSystemQpsFileWriter writer = new();

        try
        {
            await writer.WriteAsync(vaultFilePath, qpsBytes, CancellationToken.None);

            Assert.True(File.Exists(vaultFilePath));
            Assert.Equal(qpsBytes, await File.ReadAllBytesAsync(vaultFilePath));
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact]
    public async Task FileSystemWriterOverwritesExistingVaultFileAtomically()
    {
        byte[] initialBytes = await CreateValidQpsBytes([0x01, 0x02, 0x03]);
        byte[] replacementBytes = await CreateValidQpsBytes([0x04, 0x05, 0x06, 0x07]);
        string directoryPath = CreateUniqueTempDirectoryPath();
        string vaultFilePath = Path.Combine(directoryPath, "vault.qps");
        FileSystemQpsFileWriter writer = new();

        try
        {
            await writer.WriteAsync(vaultFilePath, initialBytes, CancellationToken.None);
            await writer.WriteAsync(vaultFilePath, replacementBytes, CancellationToken.None);

            Assert.Equal(replacementBytes, await File.ReadAllBytesAsync(vaultFilePath));
            Assert.Empty(Directory.EnumerateFiles(directoryPath, "*.tmp"));
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact]
    public async Task FileSystemWriterRejectsEmptyPayload()
    {
        string directoryPath = CreateUniqueTempDirectoryPath();
        string vaultFilePath = Path.Combine(directoryPath, "vault.qps");
        FileSystemQpsFileWriter writer = new();

        try
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await writer.WriteAsync(vaultFilePath, ReadOnlyMemory<byte>.Empty, CancellationToken.None));

            Assert.False(File.Exists(vaultFilePath));
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task<byte[]> CreateValidQpsBytes(byte[]? ciphertext = null)
    {
        CreateQpsVaultFileCommandHandler handler = new();
        return await handler.Handle(
            new CreateQpsVaultFileCommand(
                ValidSalt,
                ValidInitializationVector,
                ciphertext ?? ValidCiphertext,
                ValidAuthenticationTag),
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

    private sealed class RecordingQpsFileWriter : IQpsFileWriter
    {
        public bool WasCalled { get; private set; }

        public string? FilePath { get; private set; }

        public byte[]? FileBytes { get; private set; }

        public Task WriteAsync(string filePath, ReadOnlyMemory<byte> fileBytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WasCalled = true;
            FilePath = filePath;
            FileBytes = fileBytes.ToArray();

            return Task.CompletedTask;
        }
    }
}
