using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Persistence;

public sealed class ListVaultNodesQuery : IQuery<IReadOnlyList<VaultNodeRecord>>
{
    public ListVaultNodesQuery(DbConnection connection)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public DbConnection Connection { get; }
}
