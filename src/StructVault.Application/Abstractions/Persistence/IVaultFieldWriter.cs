using System.Data.Common;
using StructVault.Application.Persistence;

namespace StructVault.Application.Abstractions.Persistence;

public interface IVaultFieldWriter
{
    Task CreateAsync(DbConnection connection, CreateVaultFieldCommand field, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(DbConnection connection, UpdateVaultFieldCommand field, CancellationToken cancellationToken);

    Task DeleteAsync(DbConnection connection, DeleteVaultFieldCommand field, CancellationToken cancellationToken);
}
