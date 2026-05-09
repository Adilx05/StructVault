using System.Data.Common;
using StructVault.Application.Persistence;

namespace StructVault.Application.Abstractions.Persistence;

public interface IVaultSettingReader
{
    Task<IReadOnlyList<VaultSettingRecord>> ListByNamesAsync(
        DbConnection connection,
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken);
}
