using System.Data;
using System.Data.Common;
using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class CreateInMemoryVaultDatabaseCommandHandler : IRequestHandler<CreateInMemoryVaultDatabaseCommand, DbConnection>
{
    private readonly IVaultDatabaseConnectionFactory connectionFactory;

    public CreateInMemoryVaultDatabaseCommandHandler(IVaultDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<DbConnection> Handle(CreateInMemoryVaultDatabaseCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        DbConnection? connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            throw new InvalidOperationException("The in-memory vault database connection factory returned no connection.");
        }

        if (connection.State != ConnectionState.Open)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("The in-memory vault database connection factory returned a connection that is not open.");
        }

        return connection;
    }
}
