using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;
using StructVault.Application.Errors;

namespace StructVault.Application.Qps;

public sealed class TrySaveQpsVaultFileCommand : ICommand<VaultOperationResult>
{
    public TrySaveQpsVaultFileCommand(DbConnection connection, string filePath, string password)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        Password = password ?? throw new ArgumentNullException(nameof(password));
    }

    public DbConnection Connection { get; }

    public string FilePath { get; }

    public string Password { get; }
}
