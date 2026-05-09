using System.Data.Common;
using StructVault.Application.Persistence;

namespace StructVault.Application.Abstractions.Persistence;

public interface IVaultNodeReader
{
    Task<VaultNodeRecord?> GetByIdAsync(DbConnection connection, GetVaultNodeByIdQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<VaultNodeRecord>> ListAsync(DbConnection connection, ListVaultNodesQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<VaultSearchResultRecord>> SearchAsync(DbConnection connection, SearchVaultQuery query, CancellationToken cancellationToken);
}
