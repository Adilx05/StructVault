using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Qps;

public sealed class ChangeQpsVaultMasterPasswordCommand : ICommand
{
    public ChangeQpsVaultMasterPasswordCommand(string filePath, string currentPassword, string newPassword)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        CurrentPassword = currentPassword ?? throw new ArgumentNullException(nameof(currentPassword));
        NewPassword = newPassword ?? throw new ArgumentNullException(nameof(newPassword));
    }

    public string FilePath { get; }

    public string CurrentPassword { get; }

    public string NewPassword { get; }
}
