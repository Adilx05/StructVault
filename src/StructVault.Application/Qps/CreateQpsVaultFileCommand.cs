using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Qps;

public sealed class CreateQpsVaultFileCommand : ICommand<byte[]>
{
    private readonly byte[] salt;
    private readonly byte[] initializationVector;
    private readonly byte[] ciphertext;
    private readonly byte[] authenticationTag;

    public CreateQpsVaultFileCommand(
        byte[] salt,
        byte[] initializationVector,
        byte[] ciphertext,
        byte[] authenticationTag)
    {
        this.salt = salt?.ToArray() ?? throw new ArgumentNullException(nameof(salt));
        this.initializationVector = initializationVector?.ToArray() ?? throw new ArgumentNullException(nameof(initializationVector));
        this.ciphertext = ciphertext?.ToArray() ?? throw new ArgumentNullException(nameof(ciphertext));
        this.authenticationTag = authenticationTag?.ToArray() ?? throw new ArgumentNullException(nameof(authenticationTag));
    }

    public ReadOnlyMemory<byte> Salt => salt;

    public ReadOnlyMemory<byte> InitializationVector => initializationVector;

    public ReadOnlyMemory<byte> Ciphertext => ciphertext;

    public ReadOnlyMemory<byte> AuthenticationTag => authenticationTag;
}
