using System.Buffers.Binary;
using StructVault.Application.Qps;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class QpsFileHeaderParsingTests
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
    public async Task ParseHeaderReturnsValidatedQpsMetadata()
    {
        byte[] fileBytes = await CreateValidFileBytes();
        ParseQpsVaultFileHeaderQueryHandler handler = new();

        QpsHeader header = await handler.Handle(new ParseQpsVaultFileHeaderQuery(fileBytes), CancellationToken.None);

        Assert.Equal(QpsFileFormat.CurrentVersion, header.Version);
        Assert.Equal(ValidSalt.Length, header.SaltLength);
        Assert.Equal(ValidInitializationVector.Length, header.InitializationVectorLength);
        Assert.Equal(ValidAuthenticationTag.Length, header.AuthenticationTagLength);
        Assert.Equal(ValidCiphertext.Length, header.CiphertextLength);
        Assert.Equal(fileBytes.Length, header.RequiredFileLength);
    }

    [Fact]
    public async Task ParseHeaderRejectsTruncatedHeader()
    {
        ParseQpsVaultFileHeaderQueryHandler handler = new();
        byte[] truncatedHeader = new byte[QpsFileFormat.HeaderSizeInBytes - 1];

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ParseQpsVaultFileHeaderQuery(truncatedHeader), CancellationToken.None));
    }

    [Fact]
    public async Task ParseHeaderRejectsUnsupportedMagic()
    {
        byte[] fileBytes = await CreateValidFileBytes();
        fileBytes[0] = (byte)'X';
        ParseQpsVaultFileHeaderQueryHandler handler = new();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ParseQpsVaultFileHeaderQuery(fileBytes), CancellationToken.None));
    }

    [Fact]
    public async Task ParseHeaderRejectsUnsupportedVersion()
    {
        byte[] fileBytes = await CreateValidFileBytes();
        fileBytes[4] = QpsFileFormat.CurrentVersion + 1;
        ParseQpsVaultFileHeaderQueryHandler handler = new();

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await handler.Handle(new ParseQpsVaultFileHeaderQuery(fileBytes), CancellationToken.None));
    }

    [Fact]
    public async Task ParseHeaderRejectsInvalidSecurityComponentLengths()
    {
        byte[] invalidSaltLength = await CreateValidFileBytes();
        BinaryPrimitives.WriteUInt16BigEndian(
            invalidSaltLength.AsSpan(5, 2),
            (ushort)(QpsFileFormat.MinimumSaltSizeInBytes - 1));

        byte[] invalidInitializationVectorLength = await CreateValidFileBytes();
        invalidInitializationVectorLength[7] = QpsFileFormat.InitializationVectorSizeInBytes - 1;

        byte[] invalidAuthenticationTagLength = await CreateValidFileBytes();
        invalidAuthenticationTagLength[8] = QpsFileFormat.AuthenticationTagSizeInBytes - 1;

        byte[] emptyCiphertext = await CreateValidFileBytes();
        BinaryPrimitives.WriteUInt32BigEndian(emptyCiphertext.AsSpan(9, 4), 0);

        ParseQpsVaultFileHeaderQueryHandler handler = new();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ParseQpsVaultFileHeaderQuery(invalidSaltLength), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ParseQpsVaultFileHeaderQuery(invalidInitializationVectorLength), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ParseQpsVaultFileHeaderQuery(invalidAuthenticationTagLength), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ParseQpsVaultFileHeaderQuery(emptyCiphertext), CancellationToken.None));
    }

    [Fact]
    public async Task ParseHeaderRejectsPayloadLengthMismatch()
    {
        byte[] fileBytes = await CreateValidFileBytes();
        BinaryPrimitives.WriteUInt32BigEndian(fileBytes.AsSpan(9, 4), (uint)(ValidCiphertext.Length + 1));
        ParseQpsVaultFileHeaderQueryHandler handler = new();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ParseQpsVaultFileHeaderQuery(fileBytes), CancellationToken.None));
    }

    [Fact]
    public async Task ParseHeaderHonorsCancellationBeforeParsing()
    {
        byte[] fileBytes = await CreateValidFileBytes();
        ParseQpsVaultFileHeaderQueryHandler handler = new();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await handler.Handle(new ParseQpsVaultFileHeaderQuery(fileBytes), cancellation.Token));
    }

    [Fact]
    public async Task ParseHeaderQueryCopiesFileBytesDefensively()
    {
        byte[] fileBytes = await CreateValidFileBytes();
        ParseQpsVaultFileHeaderQuery query = new(fileBytes);
        fileBytes[0] = (byte)'X';
        ParseQpsVaultFileHeaderQueryHandler handler = new();

        QpsHeader header = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(QpsFileFormat.CurrentVersion, header.Version);
        Assert.Equal(ValidCiphertext.Length, header.CiphertextLength);
    }

    [Fact]
    public void QpsHeaderRejectsInvalidMetadata()
    {
        Assert.Throws<ArgumentException>(() => new QpsHeader(
            QpsFileFormat.CurrentVersion,
            QpsFileFormat.MinimumSaltSizeInBytes - 1,
            QpsFileFormat.InitializationVectorSizeInBytes,
            QpsFileFormat.AuthenticationTagSizeInBytes,
            ValidCiphertext.Length));

        Assert.Throws<ArgumentException>(() => new QpsHeader(
            QpsFileFormat.CurrentVersion,
            QpsFileFormat.MinimumSaltSizeInBytes,
            QpsFileFormat.InitializationVectorSizeInBytes,
            QpsFileFormat.AuthenticationTagSizeInBytes,
            0));
    }

    private static async Task<byte[]> CreateValidFileBytes()
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
}
