using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Persistence;

public sealed class DeserializeVaultDatabaseCommand : ICommand<DbConnection>
{
    public DeserializeVaultDatabaseCommand(byte[] databaseImage)
    {
        if (databaseImage is null)
        {
            throw new ArgumentNullException(nameof(databaseImage));
        }

        if (databaseImage.Length == 0)
        {
            throw new ArgumentException("Serialized vault database image cannot be empty.", nameof(databaseImage));
        }

        DatabaseImage = databaseImage;
    }

    public byte[] DatabaseImage { get; }
}
