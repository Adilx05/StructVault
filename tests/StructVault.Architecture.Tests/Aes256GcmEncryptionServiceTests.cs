using System.Security.Cryptography;
using System.Text;
using StructVault.Application.Abstractions.Security;
using StructVault.Application.Security;
using StructVault.Infrastructure.Security;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class Aes256GcmEncryptionServiceTests
{
    private static readonly byte[] ValidKey =
    [
        0x00, 0x01, 0x02, 0x03,
        0x04, 0x05, 0x06, 0x07,
        0x08, 0x09, 0x0A, 0x0B,
        0x0C, 0x0D, 0x0E, 0x0F,
        0x10, 0x11, 0x12, 0x13,
        0x14, 0x15, 0x16, 0x17,
        0x18, 0x19, 0x1A, 0x1B,
        0x1C, 0x1D, 0x1E, 0x1F,
    ];

    [Fact]
    public void EncryptThenDecryptReturnsOriginalData()
    {
        Aes256GcmEncryptionService service = new();
        byte[] plaintext = Encoding.UTF8.GetBytes("structured vault data");

        AesGcmEncryptionResult encrypted = service.Encrypt(plaintext, ValidKey);
        byte[] decrypted = service.Decrypt(
            encrypted.Ciphertext,
            ValidKey,
            encrypted.Nonce,
            encrypted.Tag);

        Assert.Equal(plaintext, decrypted);
        Assert.NotEqual(plaintext, encrypted.Ciphertext.ToArray());
        Assert.Equal(Aes256GcmEncryptionService.RecommendedNonceSizeInBytes, encrypted.Nonce.Length);
        Assert.Equal(Aes256GcmEncryptionService.AuthenticationTagSizeInBytes, encrypted.Tag.Length);
        Assert.Equal(32, service.KeySizeInBytes);
    }

    [Fact]
    public void EncryptThenDecryptSupportsEmptyData()
    {
        Aes256GcmEncryptionService service = new();
        byte[] plaintext = [];

        AesGcmEncryptionResult encrypted = service.Encrypt(plaintext, ValidKey);
        byte[] decrypted = service.Decrypt(
            encrypted.Ciphertext,
            ValidKey,
            encrypted.Nonce,
            encrypted.Tag);

        Assert.Empty(encrypted.Ciphertext.ToArray());
        Assert.Empty(decrypted);
    }

    [Fact]
    public void EncryptUsesUniqueNonceForEachOperation()
    {
        Aes256GcmEncryptionService service = new();
        byte[] plaintext = Encoding.UTF8.GetBytes("same plaintext");

        AesGcmEncryptionResult first = service.Encrypt(plaintext, ValidKey);
        AesGcmEncryptionResult second = service.Encrypt(plaintext, ValidKey);

        Assert.NotEqual(first.Nonce.ToArray(), second.Nonce.ToArray());
        Assert.NotEqual(first.Ciphertext.ToArray(), second.Ciphertext.ToArray());
    }

    [Fact]
    public void DecryptFailsWhenCiphertextIsTampered()
    {
        Aes256GcmEncryptionService service = new();
        AesGcmEncryptionResult encrypted = service.Encrypt(Encoding.UTF8.GetBytes("vault"), ValidKey);
        byte[] tamperedCiphertext = encrypted.Ciphertext.ToArray();
        tamperedCiphertext[0] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() => service.Decrypt(
            tamperedCiphertext,
            ValidKey,
            encrypted.Nonce,
            encrypted.Tag));
    }

    [Fact]
    public void DecryptFailsWhenTagIsTampered()
    {
        Aes256GcmEncryptionService service = new();
        AesGcmEncryptionResult encrypted = service.Encrypt(Encoding.UTF8.GetBytes("vault"), ValidKey);
        byte[] tamperedTag = encrypted.Tag.ToArray();
        tamperedTag[0] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() => service.Decrypt(
            encrypted.Ciphertext,
            ValidKey,
            encrypted.Nonce,
            tamperedTag));
    }

    [Fact]
    public void EncryptRequiresAes256Key()
    {
        Aes256GcmEncryptionService service = new();

        Assert.Throws<ArgumentException>(() => service.Encrypt(new byte[] { 1, 2, 3 }, new byte[31]));
    }

    [Fact]
    public void DecryptRequiresValidNonceAndTagLengths()
    {
        Aes256GcmEncryptionService service = new();

        Assert.Throws<ArgumentException>(() => service.Decrypt(Array.Empty<byte>(), ValidKey, new byte[11], new byte[16]));
        Assert.Throws<ArgumentException>(() => service.Decrypt(Array.Empty<byte>(), ValidKey, new byte[12], new byte[15]));
    }

    [Fact]
    public async Task EncryptHandlerEncryptsThroughApplicationContract()
    {
        AesGcmEncryptionResult expected = new(new byte[12], [4, 5, 6], new byte[16]);
        RecordingEncryptionService service = new(expected, [10, 11, 12]);
        EncryptVaultDataCommandHandler handler = new(service);
        EncryptVaultDataCommand command = new([0xAA], ValidKey);

        AesGcmEncryptionResult actual = await handler.Handle(command, CancellationToken.None);

        Assert.Same(expected, actual);
        Assert.Equal([0xAA], service.Plaintext);
        Assert.Equal(ValidKey, service.EncryptionKey);
    }

    [Fact]
    public async Task DecryptHandlerDecryptsThroughApplicationContract()
    {
        byte[] expectedPlaintext = [10, 11, 12];
        RecordingEncryptionService service = new(new AesGcmEncryptionResult(new byte[12], [2], new byte[16]), expectedPlaintext);
        DecryptVaultDataCommandHandler handler = new(service);
        DecryptVaultDataCommand command = new([0xAA], ValidKey, new byte[12], new byte[16]);

        byte[] actual = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(expectedPlaintext, actual);
        Assert.Equal([0xAA], service.Ciphertext);
        Assert.Equal(ValidKey, service.DecryptionKey);
        Assert.Equal(new byte[12], service.Nonce);
        Assert.Equal(new byte[16], service.Tag);
    }

    [Fact]
    public async Task EncryptHandlerRejectsInvalidKeyBeforeEncrypting()
    {
        RecordingEncryptionService service = new(new AesGcmEncryptionResult(new byte[12], [2], new byte[16]), [4]);
        EncryptVaultDataCommandHandler handler = new(service);
        EncryptVaultDataCommand command = new([0xAA], new byte[31]);

        await Assert.ThrowsAsync<ArgumentException>(async () => await handler.Handle(command, CancellationToken.None));

        Assert.False(service.WasEncryptCalled);
    }

    [Fact]
    public void CommandsCopySecurityInputsDefensively()
    {
        byte[] plaintext = [1, 2, 3];
        byte[] key = ValidKey.ToArray();
        byte[] ciphertext = [4, 5, 6];
        byte[] nonce = new byte[12];
        byte[] tag = new byte[16];

        EncryptVaultDataCommand encryptCommand = new(plaintext, key);
        DecryptVaultDataCommand decryptCommand = new(ciphertext, key, nonce, tag);

        plaintext[0] = 0xFF;
        key[0] = 0xFF;
        ciphertext[0] = 0xFF;
        nonce[0] = 0xFF;
        tag[0] = 0xFF;

        Assert.Equal([1, 2, 3], encryptCommand.Plaintext.ToArray());
        Assert.Equal(ValidKey, encryptCommand.Key.ToArray());
        Assert.Equal([4, 5, 6], decryptCommand.Ciphertext.ToArray());
        Assert.Equal(ValidKey, decryptCommand.Key.ToArray());
        Assert.Equal(new byte[12], decryptCommand.Nonce.ToArray());
        Assert.Equal(new byte[16], decryptCommand.Tag.ToArray());
    }

    private sealed class RecordingEncryptionService : IAuthenticatedEncryptionService
    {
        private readonly AesGcmEncryptionResult encryptionResult;
        private readonly byte[] plaintextResult;

        public RecordingEncryptionService(AesGcmEncryptionResult encryptionResult, byte[] plaintextResult)
        {
            this.encryptionResult = encryptionResult;
            this.plaintextResult = plaintextResult;
        }

        public int KeySizeInBytes => Aes256GcmEncryptionService.Aes256KeySizeInBytes;

        public int NonceSizeInBytes => Aes256GcmEncryptionService.RecommendedNonceSizeInBytes;

        public int TagSizeInBytes => Aes256GcmEncryptionService.AuthenticationTagSizeInBytes;

        public byte[]? Plaintext { get; private set; }

        public byte[]? EncryptionKey { get; private set; }

        public byte[]? Ciphertext { get; private set; }

        public byte[]? DecryptionKey { get; private set; }

        public byte[]? Nonce { get; private set; }

        public byte[]? Tag { get; private set; }

        public bool WasEncryptCalled { get; private set; }

        public AesGcmEncryptionResult Encrypt(ReadOnlyMemory<byte> plaintext, ReadOnlyMemory<byte> key)
        {
            WasEncryptCalled = true;
            Plaintext = plaintext.ToArray();
            EncryptionKey = key.ToArray();
            return encryptionResult;
        }

        public byte[] Decrypt(
            ReadOnlyMemory<byte> ciphertext,
            ReadOnlyMemory<byte> key,
            ReadOnlyMemory<byte> nonce,
            ReadOnlyMemory<byte> tag)
        {
            Ciphertext = ciphertext.ToArray();
            DecryptionKey = key.ToArray();
            Nonce = nonce.ToArray();
            Tag = tag.ToArray();
            return plaintextResult.ToArray();
        }
    }
}
