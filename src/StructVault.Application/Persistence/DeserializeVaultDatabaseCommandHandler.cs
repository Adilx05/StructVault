using System.Data;
using System.Data.Common;
using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class DeserializeVaultDatabaseCommandHandler : IRequestHandler<DeserializeVaultDatabaseCommand, DbConnection>
{
    private readonly IVaultDatabaseSerializer databaseSerializer;

    public DeserializeVaultDatabaseCommandHandler(IVaultDatabaseSerializer databaseSerializer)
    {
        this.databaseSerializer = databaseSerializer ?? throw new ArgumentNullException(nameof(databaseSerializer));
    }

    public async Task<DbConnection> Handle(DeserializeVaultDatabaseCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        DbConnection? connection = await databaseSerializer.DeserializeAsync(request.DatabaseImage, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            throw new InvalidOperationException("Vault database serializer returned no deserialized SQLite connection.");
        }

        if (connection.State != ConnectionState.Open)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("Vault database serializer returned a connection that is not open.");
        }

        return connection;
    }
}
