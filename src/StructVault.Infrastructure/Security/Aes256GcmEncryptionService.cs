using System.Security.Cryptography;
using StructVault.Application.Abstractions.Security;

namespace StructVault.Infrastructure.Security;

public sealed class Aes256GcmEncryptionService : IAuthenticatedEncryptionService
{
    public const int Aes256KeySizeInBytes = 32;
    public const int RecommendedNonceSizeInBytes = 12;
    public const int AuthenticationTagSizeInBytes = 16;

    public int KeySizeInBytes => Aes256KeySizeInBytes;

    public int NonceSizeInBytes => RecommendedNonceSizeInBytes;

    public int TagSizeInBytes => AuthenticationTagSizeInBytes;

    public AesGcmEncryptionResult Encrypt(ReadOnlyMemory<byte> plaintext, ReadOnlyMemory<byte> key)
    {
        ValidateKey(key);

        byte[] nonce = RandomNumberGenerator.GetBytes(RecommendedNonceSizeInBytes);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[AuthenticationTagSizeInBytes];
        byte[] keyBytes = key.ToArray();

        try
        {
            using AesGcm aesGcm = new(keyBytes, AuthenticationTagSizeInBytes);
            aesGcm.Encrypt(nonce, plaintext.Span, ciphertext, tag);
            return new AesGcmEncryptionResult(nonce, ciphertext, tag);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }

    public byte[] Decrypt(
        ReadOnlyMemory<byte> ciphertext,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> tag)
    {
        ValidateKey(key);
        ValidateNonce(nonce);
        ValidateTag(tag);

        byte[] plaintext = new byte[ciphertext.Length];
        byte[] keyBytes = key.ToArray();

        try
        {
            using AesGcm aesGcm = new(keyBytes, AuthenticationTagSizeInBytes);
            aesGcm.Decrypt(nonce.Span, ciphertext.Span, tag.Span, plaintext);
            return plaintext;
        }
        catch (AuthenticationTagMismatchException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }

    private static void ValidateKey(ReadOnlyMemory<byte> key)
    {
        if (key.Length != Aes256KeySizeInBytes)
        {
            throw new ArgumentException($"AES-256-GCM requires a {Aes256KeySizeInBytes}-byte key.", nameof(key));
        }
    }

    private static void ValidateNonce(ReadOnlyMemory<byte> nonce)
    {
        if (nonce.Length != RecommendedNonceSizeInBytes)
        {
            throw new ArgumentException($"AES-GCM requires a {RecommendedNonceSizeInBytes}-byte nonce.", nameof(nonce));
        }
    }

    private static void ValidateTag(ReadOnlyMemory<byte> tag)
    {
        if (tag.Length != AuthenticationTagSizeInBytes)
        {
            throw new ArgumentException($"AES-GCM requires a {AuthenticationTagSizeInBytes}-byte authentication tag.", nameof(tag));
        }
    }
}
