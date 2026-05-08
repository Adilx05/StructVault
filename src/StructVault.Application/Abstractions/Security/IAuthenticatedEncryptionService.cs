namespace StructVault.Application.Abstractions.Security;

public interface IAuthenticatedEncryptionService
{
    int KeySizeInBytes { get; }

    int NonceSizeInBytes { get; }

    int TagSizeInBytes { get; }

    AesGcmEncryptionResult Encrypt(ReadOnlyMemory<byte> plaintext, ReadOnlyMemory<byte> key);

    byte[] Decrypt(
        ReadOnlyMemory<byte> ciphertext,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> tag);
}
