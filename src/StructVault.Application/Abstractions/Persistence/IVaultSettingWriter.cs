using System.Data.Common;
using StructVault.Application.Persistence;

namespace StructVault.Application.Abstractions.Persistence;

public interface IVaultSettingWriter
{
    Task UpsertManyAsync(
        DbConnection connection,
        IReadOnlyCollection<VaultSettingRecord> settings,
        CancellationToken cancellationToken);
}
