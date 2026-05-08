using System.Buffers.Binary;
using StructVault.Application.Qps;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class QpsFileFormatTests
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
    public async Task CreateThenParseQpsVaultFilePreservesEncryptedVaultEnvelope()
    {
        CreateQpsVaultFileCommandHandler createHandler = new();
        ParseQpsVaultFileQueryHandler parseHandler = new();
        CreateQpsVaultFileCommand command = new(
            ValidSalt,
            ValidInitializationVector,
            ValidCiphertext,
            ValidAuthenticationTag);

        byte[] fileBytes = await createHandler.Handle(command, CancellationToken.None);
        QpsVaultFile parsed = await parseHandler.Handle(new ParseQpsVaultFileQuery(fileBytes), CancellationToken.None);

        Assert.Equal(QpsFileFormat.CurrentVersion, parsed.Version);
        Assert.Equal(ValidSalt, parsed.Salt.ToArray());
        Assert.Equal(ValidInitializationVector, parsed.InitializationVector.ToArray());
        Assert.Equal(ValidCiphertext, parsed.Ciphertext.ToArray());
        Assert.Equal(ValidAuthenticationTag, parsed.AuthenticationTag.ToArray());
    }

    [Fact]
    public async Task CreateWritesDeterministicHeaderBeforeEncryptedVaultData()
    {
        CreateQpsVaultFileCommandHandler handler = new();

        byte[] fileBytes = await handler.Handle(
            new CreateQpsVaultFileCommand(
                ValidSalt,
                ValidInitializationVector,
                ValidCiphertext,
                ValidAuthenticationTag),
            CancellationToken.None);

        Assert.Equal((byte)'Q', fileBytes[0]);
        Assert.Equal((byte)'P', fileBytes[1]);
        Assert.Equal((byte)'S', fileBytes[2]);
        Assert.Equal((byte)'V', fileBytes[3]);
        Assert.Equal(QpsFileFormat.CurrentVersion, fileBytes[4]);
        Assert.Equal((ushort)ValidSalt.Length, BinaryPrimitives.ReadUInt16BigEndian(fileBytes.AsSpan(5, 2)));
        Assert.Equal((byte)ValidInitializationVector.Length, fileBytes[7]);
        Assert.Equal((byte)ValidAuthenticationTag.Length, fileBytes[8]);
        Assert.Equal((uint)ValidCiphertext.Length, BinaryPrimitives.ReadUInt32BigEndian(fileBytes.AsSpan(9, 4)));
        Assert.Equal(QpsFileFormat.HeaderSizeInBytes, 13);
    }

    [Fact]
    public async Task ParseRejectsInvalidMagicHeader()
    {
        byte[] fileBytes = await CreateValidFileBytes();
        fileBytes[0] = (byte)'X';
        ParseQpsVaultFileQueryHandler handler = new();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ParseQpsVaultFileQuery(fileBytes), CancellationToken.None));
    }

    [Fact]
    public async Task ParseRejectsUnsupportedVersion()
    {
        byte[] fileBytes = await CreateValidFileBytes();
        fileBytes[4] = QpsFileFormat.CurrentVersion + 1;
        ParseQpsVaultFileQueryHandler handler = new();

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await handler.Handle(new ParseQpsVaultFileQuery(fileBytes), CancellationToken.None));
    }

    [Fact]
    public async Task ParseRejectsTruncatedHeader()
    {
        ParseQpsVaultFileQueryHandler handler = new();
        byte[] truncatedHeader = new byte[QpsFileFormat.HeaderSizeInBytes - 1];

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ParseQpsVaultFileQuery(truncatedHeader), CancellationToken.None));
    }

    [Fact]
    public async Task ParseRejectsLengthMismatch()
    {
        byte[] fileBytes = await CreateValidFileBytes();
        byte[] truncatedFile = fileBytes[..^1];
        ParseQpsVaultFileQueryHandler handler = new();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ParseQpsVaultFileQuery(truncatedFile), CancellationToken.None));
    }

    [Fact]
    public async Task ParseRejectsInvalidSecurityComponentLengths()
    {
        byte[] invalidSaltLength = await CreateValidFileBytes();
        BinaryPrimitives.WriteUInt16BigEndian(invalidSaltLength.AsSpan(5, 2), (ushort)(QpsFileFormat.MinimumSaltSizeInBytes - 1));

        byte[] invalidInitializationVectorLength = await CreateValidFileBytes();
        invalidInitializationVectorLength[7] = QpsFileFormat.InitializationVectorSizeInBytes - 1;

        byte[] invalidAuthenticationTagLength = await CreateValidFileBytes();
        invalidAuthenticationTagLength[8] = QpsFileFormat.AuthenticationTagSizeInBytes - 1;

        ParseQpsVaultFileQueryHandler handler = new();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ParseQpsVaultFileQuery(invalidSaltLength), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ParseQpsVaultFileQuery(invalidInitializationVectorLength), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new ParseQpsVaultFileQuery(invalidAuthenticationTagLength), CancellationToken.None));
    }

    [Fact]
    public async Task CreateRejectsInvalidSecurityInputsBeforeWritingBytes()
    {
        CreateQpsVaultFileCommandHandler handler = new();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new CreateQpsVaultFileCommand(
                new byte[QpsFileFormat.MinimumSaltSizeInBytes - 1],
                ValidInitializationVector,
                ValidCiphertext,
                ValidAuthenticationTag), CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new CreateQpsVaultFileCommand(
                ValidSalt,
                new byte[QpsFileFormat.InitializationVectorSizeInBytes - 1],
                ValidCiphertext,
                ValidAuthenticationTag), CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new CreateQpsVaultFileCommand(
                ValidSalt,
                ValidInitializationVector,
                ValidCiphertext,
                new byte[QpsFileFormat.AuthenticationTagSizeInBytes - 1]), CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await handler.Handle(new CreateQpsVaultFileCommand(
                ValidSalt,
                ValidInitializationVector,
                [],
                ValidAuthenticationTag), CancellationToken.None));
    }

    [Fact]
    public void CommandsAndModelsCopyQpsBytesDefensively()
    {
        byte[] salt = ValidSalt.ToArray();
        byte[] initializationVector = ValidInitializationVector.ToArray();
        byte[] ciphertext = ValidCiphertext.ToArray();
        byte[] authenticationTag = ValidAuthenticationTag.ToArray();

        CreateQpsVaultFileCommand command = new(salt, initializationVector, ciphertext, authenticationTag);
        QpsVaultFile vaultFile = new(QpsFileFormat.CurrentVersion, salt, initializationVector, ciphertext, authenticationTag);

        salt[0] = 0xFF;
        initializationVector[0] = 0xFF;
        ciphertext[0] = 0xFF;
        authenticationTag[0] = 0xFF;

        Assert.Equal(ValidSalt, command.Salt.ToArray());
        Assert.Equal(ValidInitializationVector, command.InitializationVector.ToArray());
        Assert.Equal(ValidCiphertext, command.Ciphertext.ToArray());
        Assert.Equal(ValidAuthenticationTag, command.AuthenticationTag.ToArray());
        Assert.Equal(ValidSalt, vaultFile.Salt.ToArray());
        Assert.Equal(ValidInitializationVector, vaultFile.InitializationVector.ToArray());
        Assert.Equal(ValidCiphertext, vaultFile.Ciphertext.ToArray());
        Assert.Equal(ValidAuthenticationTag, vaultFile.AuthenticationTag.ToArray());
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
