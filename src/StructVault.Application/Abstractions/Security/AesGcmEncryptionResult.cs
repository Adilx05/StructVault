namespace StructVault.Application.Abstractions.Security;

public sealed class AesGcmEncryptionResult
{
    public const int NonceSizeInBytes = 12;
    public const int TagSizeInBytes = 16;

    private readonly byte[] nonce;
    private readonly byte[] ciphertext;
    private readonly byte[] tag;

    public AesGcmEncryptionResult(byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(tag);

        if (nonce.Length != NonceSizeInBytes)
        {
            throw new ArgumentException($"AES-GCM requires a {NonceSizeInBytes}-byte nonce.", nameof(nonce));
        }

        if (tag.Length != TagSizeInBytes)
        {
            throw new ArgumentException($"AES-GCM requires a {TagSizeInBytes}-byte authentication tag.", nameof(tag));
        }

        this.nonce = nonce.ToArray();
        this.ciphertext = ciphertext.ToArray();
        this.tag = tag.ToArray();
    }

    public ReadOnlyMemory<byte> Nonce => nonce;

    public ReadOnlyMemory<byte> Ciphertext => ciphertext;

    public ReadOnlyMemory<byte> Tag => tag;
}
