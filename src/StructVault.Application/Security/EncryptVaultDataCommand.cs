using StructVault.Application.Abstractions.Messaging;
using StructVault.Application.Abstractions.Security;

namespace StructVault.Application.Security;

public sealed class EncryptVaultDataCommand : ICommand<AesGcmEncryptionResult>
{
    private readonly byte[] plaintext;
    private readonly byte[] key;

    public EncryptVaultDataCommand(byte[] plaintext, byte[] key)
    {
        this.plaintext = plaintext?.ToArray() ?? throw new ArgumentNullException(nameof(plaintext));
        this.key = key?.ToArray() ?? throw new ArgumentNullException(nameof(key));
    }

    public ReadOnlyMemory<byte> Plaintext => plaintext;

    public ReadOnlyMemory<byte> Key => key;
}
