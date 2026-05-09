using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Qps;

public sealed class RestoreQpsVaultFileBackupCommand : ICommand
{
    public RestoreQpsVaultFileBackupCommand(string filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public string FilePath { get; }
}
