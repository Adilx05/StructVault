using System.Data.Common;
using StructVault.Application.Persistence;

namespace StructVault.Application.Abstractions.Persistence;

public interface IVaultNodeWriter
{
    Task CreateAsync(DbConnection connection, CreateVaultNodeCommand node, CancellationToken cancellationToken);

    Task DeleteAsync(DbConnection connection, DeleteVaultNodeCommand node, CancellationToken cancellationToken);
}
