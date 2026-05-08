using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Security;

public sealed class DecryptVaultDataCommand : ICommand<byte[]>
{
    private readonly byte[] ciphertext;
    private readonly byte[] key;
    private readonly byte[] nonce;
    private readonly byte[] tag;

    public DecryptVaultDataCommand(byte[] ciphertext, byte[] key, byte[] nonce, byte[] tag)
    {
        this.ciphertext = ciphertext?.ToArray() ?? throw new ArgumentNullException(nameof(ciphertext));
        this.key = key?.ToArray() ?? throw new ArgumentNullException(nameof(key));
        this.nonce = nonce?.ToArray() ?? throw new ArgumentNullException(nameof(nonce));
        this.tag = tag?.ToArray() ?? throw new ArgumentNullException(nameof(tag));
    }

    public ReadOnlyMemory<byte> Ciphertext => ciphertext;

    public ReadOnlyMemory<byte> Key => key;

    public ReadOnlyMemory<byte> Nonce => nonce;

    public ReadOnlyMemory<byte> Tag => tag;
}
