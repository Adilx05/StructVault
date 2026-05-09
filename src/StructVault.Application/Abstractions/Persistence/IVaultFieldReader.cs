using System.Data.Common;
using StructVault.Application.Persistence;

namespace StructVault.Application.Abstractions.Persistence;

public interface IVaultFieldReader
{
    Task<VaultFieldRecord?> GetByIdAsync(DbConnection connection, GetVaultFieldByIdQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<VaultFieldRecord>> ListByNodeIdAsync(DbConnection connection, ListVaultFieldsByNodeIdQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<VaultSearchResultRecord>> SearchAsync(DbConnection connection, SearchVaultQuery query, CancellationToken cancellationToken);
}
