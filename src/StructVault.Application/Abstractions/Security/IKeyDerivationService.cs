namespace StructVault.Application.Abstractions.Security;

public interface IKeyDerivationService
{
    int KeySizeInBytes { get; }

    byte[] DeriveKey(string password, ReadOnlyMemory<byte> salt);
}
