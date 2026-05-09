using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Persistence;

public sealed class GetClipboardSettingsQuery : IQuery<ClipboardSettingsRecord>
{
    public GetClipboardSettingsQuery(DbConnection connection)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public DbConnection Connection { get; }
}
