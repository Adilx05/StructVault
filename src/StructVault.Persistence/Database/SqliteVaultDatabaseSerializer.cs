using System.Data;
using System.Data.Common;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using StructVault.Application.Abstractions.Persistence;
using StructVault.Persistence.Schema;

namespace StructVault.Persistence.Database;

public sealed class SqliteVaultDatabaseSerializer : IVaultDatabaseSerializer
{
    private const string MainSchemaName = "main";
    private const int SqliteOk = 0;
    private const uint SqliteDeserializeFreeOnClose = 1;
    private const uint SqliteDeserializeResizeable = 2;

    private readonly IVaultSchemaProvider schemaProvider;

    public SqliteVaultDatabaseSerializer(IVaultSchemaProvider schemaProvider)
    {
        this.schemaProvider = schemaProvider ?? throw new ArgumentNullException(nameof(schemaProvider));
    }

    public async Task<byte[]> SerializeAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        cancellationToken.ThrowIfCancellationRequested();

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);
        await ValidateVaultDatabaseAsync(sqliteConnection, cancellationToken).ConfigureAwait(false);

        IntPtr imagePointer = sqlite3_serialize(sqliteConnection.Handle, MainSchemaName, out long imageSize, 0);
        if (imagePointer == IntPtr.Zero || imageSize <= 0)
        {
            throw new InvalidOperationException("SQLite did not return a serialized vault database image.");
        }

        try
        {
            if (imageSize > int.MaxValue)
            {
                throw new InvalidOperationException("Serialized vault database image is too large to load into memory.");
            }

            byte[] databaseImage = new byte[(int)imageSize];
            Marshal.Copy(imagePointer, databaseImage, 0, databaseImage.Length);
            return databaseImage;
        }
        finally
        {
            sqlite3_free(imagePointer);
        }
    }

    public async Task<DbConnection> DeserializeAsync(byte[] databaseImage, CancellationToken cancellationToken)
    {
        if (databaseImage is null)
        {
            throw new ArgumentNullException(nameof(databaseImage));
        }

        if (databaseImage.Length == 0)
        {
            throw new ArgumentException("Serialized vault database image cannot be empty.", nameof(databaseImage));
        }

        cancellationToken.ThrowIfCancellationRequested();
        EnsureSchemaConfigured();

        SqliteConnection connection = new("Data Source=:memory:");
        IntPtr imagePointer = IntPtr.Zero;

        try
        {
            string schemaScript = EnsureSchemaConfigured();

            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            imagePointer = sqlite3_malloc64((ulong)databaseImage.LongLength);
            if (imagePointer == IntPtr.Zero)
            {
                throw new InvalidOperationException("SQLite could not allocate memory for the serialized vault database image.");
            }

            Marshal.Copy(databaseImage, 0, imagePointer, databaseImage.Length);

            int result = sqlite3_deserialize(
                connection.Handle,
                MainSchemaName,
                imagePointer,
                databaseImage.LongLength,
                databaseImage.LongLength,
                SqliteDeserializeFreeOnClose | SqliteDeserializeResizeable);

            if (result != SqliteOk)
            {
                sqlite3_free(imagePointer);
                imagePointer = IntPtr.Zero;
                throw new InvalidDataException($"SQLite rejected the serialized vault database image with result code {result}.");
            }

            imagePointer = IntPtr.Zero;

            await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(connection, schemaScript, cancellationToken).ConfigureAwait(false);
            await ValidateVaultDatabaseAsync(connection, cancellationToken).ConfigureAwait(false);

            return connection;
        }
        catch (SqliteException exception)
        {
            if (imagePointer != IntPtr.Zero)
            {
                sqlite3_free(imagePointer);
            }

            await connection.DisposeAsync().ConfigureAwait(false);
            throw new InvalidDataException("SQLite vault database image could not be deserialized.", exception);
        }
        catch
        {
            if (imagePointer != IntPtr.Zero)
            {
                sqlite3_free(imagePointer);
            }

            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static SqliteConnection RequireOpenSqliteConnection(DbConnection connection)
    {
        if (connection is not SqliteConnection sqliteConnection)
        {
            throw new ArgumentException("Vault database serialization requires a SQLite connection.", nameof(connection));
        }

        if (sqliteConnection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Vault database serialization requires an open SQLite connection.");
        }

        return sqliteConnection;
    }

    private async Task ValidateVaultDatabaseAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        EnsureSchemaConfigured();

        try
        {
            string integrityResult = Convert.ToString(await ExecuteScalarAsync(connection, "PRAGMA quick_check;", cancellationToken).ConfigureAwait(false))
                ?? string.Empty;
            if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("SQLite vault database image failed integrity validation.");
            }

            bool hasNodeTable = await TableExistsAsync(connection, VaultSchema.VaultNodeTableName, cancellationToken).ConfigureAwait(false);
            bool hasFieldTable = await TableExistsAsync(connection, VaultSchema.VaultFieldTableName, cancellationToken).ConfigureAwait(false);
            bool hasSettingTable = await TableExistsAsync(connection, VaultSchema.VaultSettingTableName, cancellationToken).ConfigureAwait(false);
            if (!hasNodeTable || !hasFieldTable || !hasSettingTable)
            {
                throw new InvalidDataException("SQLite vault database image does not contain the required vault schema.");
            }
        }
        catch (SqliteException exception)
        {
            throw new InvalidDataException("SQLite vault database image could not be validated.", exception);
        }
    }

    private string EnsureSchemaConfigured()
    {
        string schemaScript = schemaProvider.GetCreateSchemaScript();
        if (string.IsNullOrWhiteSpace(schemaScript))
        {
            throw new InvalidOperationException("Vault schema provider returned an empty SQLite schema script.");
        }

        return schemaScript;
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        command.Parameters.AddWithValue("$tableName", tableName);

        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result) == 1L;
    }

    private static async Task<object?> ExecuteScalarAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
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

    [DllImport("e_sqlite3", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern IntPtr sqlite3_serialize(
        sqlite3 database,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string schema,
        out long size,
        uint flags);

    [DllImport("e_sqlite3", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int sqlite3_deserialize(
        sqlite3 database,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string schema,
        IntPtr data,
        long size,
        long bufferSize,
        uint flags);

    [DllImport("e_sqlite3", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern IntPtr sqlite3_malloc64(ulong size);

    [DllImport("e_sqlite3", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void sqlite3_free(IntPtr pointer);
}
