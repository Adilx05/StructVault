using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Security;

public sealed class DeriveVaultKeyCommand : ICommand<byte[]>
{
    private readonly byte[] salt;

    public DeriveVaultKeyCommand(string password, byte[] salt)
    {
        Password = password ?? throw new ArgumentNullException(nameof(password));
        this.salt = salt?.ToArray() ?? throw new ArgumentNullException(nameof(salt));
    }

    public string Password { get; }

    public ReadOnlyMemory<byte> Salt => salt;
}
