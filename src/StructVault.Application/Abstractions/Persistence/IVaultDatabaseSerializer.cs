using System.Data.Common;

namespace StructVault.Application.Abstractions.Persistence;

public interface IVaultDatabaseSerializer
{
    Task<byte[]> SerializeAsync(DbConnection connection, CancellationToken cancellationToken);

    Task<DbConnection> DeserializeAsync(byte[] databaseImage, CancellationToken cancellationToken);
}
