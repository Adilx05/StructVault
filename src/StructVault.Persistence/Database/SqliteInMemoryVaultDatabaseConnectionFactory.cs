using System.Data.Common;
using Microsoft.Data.Sqlite;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Persistence.Database;

public sealed class SqliteInMemoryVaultDatabaseConnectionFactory : IVaultDatabaseConnectionFactory
{
    private const string InMemoryConnectionString = "Data Source=:memory:";

    private readonly IVaultSchemaProvider schemaProvider;

    public SqliteInMemoryVaultDatabaseConnectionFactory(IVaultSchemaProvider schemaProvider)
    {
        this.schemaProvider = schemaProvider ?? throw new ArgumentNullException(nameof(schemaProvider));
    }

    public async Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string schemaScript = schemaProvider.GetCreateSchemaScript();
        if (string.IsNullOrWhiteSpace(schemaScript))
        {
            throw new InvalidOperationException("Vault schema provider returned an empty SQLite schema script.");
        }

        SqliteConnection connection = new(InMemoryConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(connection, schemaScript, cancellationToken).ConfigureAwait(false);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
