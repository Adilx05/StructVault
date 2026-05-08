using System.Data.Common;
using StructVault.Application.Persistence;

namespace StructVault.Application.Abstractions.Persistence;

public interface IVaultFieldWriter
{
    Task CreateAsync(DbConnection connection, CreateVaultFieldCommand field, CancellationToken cancellationToken);
}
