using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Persistence.Schema;

public sealed class SqliteVaultSchemaProvider : IVaultSchemaProvider
{
    public string GetCreateSchemaScript()
    {
        if (string.IsNullOrWhiteSpace(VaultSchema.CreateScript))
        {
            throw new InvalidOperationException("SQLite vault schema script is not configured.");
        }

        return VaultSchema.CreateScript;
    }
}
