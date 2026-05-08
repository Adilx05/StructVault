using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using StructVault.Application.Abstractions.Security;

namespace StructVault.Infrastructure.Security;

public sealed class Argon2idKeyDerivationService : IKeyDerivationService
{
    public const int DerivedKeySizeInBytes = 32;
    public const int MinimumSaltSizeInBytes = 16;
    public const int DefaultDegreeOfParallelism = 2;
    public const int DefaultMemorySizeInKiB = 64 * 1024;
    public const int DefaultIterations = 3;

    public int KeySizeInBytes => DerivedKeySizeInBytes;

    public byte[] DeriveKey(string password, ReadOnlyMemory<byte> salt)
    {
        Validate(password, salt);

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] saltBytes = salt.ToArray();

        try
        {
            Argon2id argon2 = new(passwordBytes)
            {
                Salt = saltBytes,
                DegreeOfParallelism = DefaultDegreeOfParallelism,
                MemorySize = DefaultMemorySizeInKiB,
                Iterations = DefaultIterations,
            };

            return argon2.GetBytes(DerivedKeySizeInBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(saltBytes);
        }
    }

    private static void Validate(string password, ReadOnlyMemory<byte> salt)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("A non-empty password is required.", nameof(password));
        }

        if (salt.Length < MinimumSaltSizeInBytes)
        {
            throw new ArgumentException($"Salt must be at least {MinimumSaltSizeInBytes} bytes.", nameof(salt));
        }
    }
}
