using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.IdleLock;

public sealed class UnlockVaultCommand : ICommand<bool>
{
    public UnlockVaultCommand(string filePath, string password)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        Password = password ?? throw new ArgumentNullException(nameof(password));
    }

    public string FilePath { get; }

    public string Password { get; }
}
