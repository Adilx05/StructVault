using StructVault.Application.Abstractions.Storage;
using StructVault.Application.Qps;
using StructVault.Infrastructure.Storage;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class QpsFileReaderTests
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
    public async Task HandlerReadsValidQpsFileThroughReader()
    {
        byte[] qpsBytes = await CreateValidQpsBytes();
        RecordingQpsFileReader reader = new(qpsBytes);
        ReadQpsVaultFileQueryHandler handler = new(reader);

        QpsVaultFile vaultFile = await handler.Handle(new ReadQpsVaultFileQuery("vault.qps"), CancellationToken.None);

        Assert.Equal("vault.qps", reader.FilePath);
        Assert.Equal(QpsFileFormat.CurrentVersion, vaultFile.Version);
        Assert.Equal(ValidSalt, vaultFile.Salt.ToArray());
        Assert.Equal(ValidInitializationVector, vaultFile.InitializationVector.ToArray());
        Assert.Equal(ValidCiphertext, vaultFile.Ciphertext.ToArray());
        Assert.Equal(ValidAuthenticationTag, vaultFile.AuthenticationTag.ToArray());
    }

    [Fact]
    public async Task HandlerRejectsBlankPathBeforeReading()
    {
        RecordingQpsFileReader reader = new(await CreateValidQpsBytes());
        ReadQpsVaultFileQueryHandler handler = new(reader);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ReadQpsVaultFileQuery(" "), CancellationToken.None));

        Assert.False(reader.WasCalled);
    }

    [Fact]
    public async Task HandlerRejectsEmptyFileBytes()
    {
        RecordingQpsFileReader reader = new([]);
        ReadQpsVaultFileQueryHandler handler = new(reader);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ReadQpsVaultFileQuery("vault.qps"), CancellationToken.None));

        Assert.True(reader.WasCalled);
    }

    [Fact]
    public async Task HandlerRejectsInvalidQpsBytes()
    {
        RecordingQpsFileReader reader = new([0x01, 0x02]);
        ReadQpsVaultFileQueryHandler handler = new(reader);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ReadQpsVaultFileQuery("vault.qps"), CancellationToken.None));

        Assert.True(reader.WasCalled);
    }

    [Fact]
    public async Task HandlerHonorsCancellationBeforeReading()
    {
        RecordingQpsFileReader reader = new(await CreateValidQpsBytes());
        ReadQpsVaultFileQueryHandler handler = new(reader);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await handler.Handle(new ReadQpsVaultFileQuery("vault.qps"), cancellation.Token));

        Assert.False(reader.WasCalled);
    }

    [Fact]
    public async Task FileSystemReaderLoadsVaultFileBytes()
    {
        byte[] qpsBytes = await CreateValidQpsBytes();
        string directoryPath = CreateUniqueTempDirectoryPath();
        string vaultFilePath = Path.Combine(directoryPath, "nested", "vault.qps");
        FileSystemQpsFileWriter writer = new();
        FileSystemQpsFileReader reader = new();

        try
        {
            await writer.WriteAsync(vaultFilePath, qpsBytes, CancellationToken.None);

            byte[] readBytes = await reader.ReadAsync(vaultFilePath, CancellationToken.None);

            Assert.Equal(qpsBytes, readBytes);
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact]
    public async Task FileSystemReaderRejectsBlankPath()
    {
        FileSystemQpsFileReader reader = new();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await reader.ReadAsync(" ", CancellationToken.None));
    }

    private static async Task<byte[]> CreateValidQpsBytes()
    {
        CreateQpsVaultFileCommandHandler handler = new();
        return await handler.Handle(
            new CreateQpsVaultFileCommand(
                ValidSalt,
                ValidInitializationVector,
                ValidCiphertext,
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

    private sealed class RecordingQpsFileReader : IQpsFileReader
    {
        private readonly byte[] fileBytes;

        public RecordingQpsFileReader(byte[] fileBytes)
        {
            this.fileBytes = fileBytes.ToArray();
        }

        public bool WasCalled { get; private set; }

        public string? FilePath { get; private set; }

        public Task<byte[]> ReadAsync(string filePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WasCalled = true;
            FilePath = filePath;

            return Task.FromResult(fileBytes.ToArray());
        }
    }
}
