using System.Data.Common;

namespace StructVault.Application.Abstractions.Persistence;

public interface IVaultDatabaseConnectionFactory
{
    Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken);
}
