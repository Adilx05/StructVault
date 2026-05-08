using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Persistence;

public sealed class ListVaultFieldsByNodeIdQuery : IQuery<IReadOnlyList<VaultFieldRecord>>
{
    public ListVaultFieldsByNodeIdQuery(DbConnection connection, string nodeId)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        NodeId = RequireNonEmpty(nodeId, nameof(nodeId));
    }

    public DbConnection Connection { get; }

    public string NodeId { get; }

    private static string RequireNonEmpty(string value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Value cannot be empty or whitespace.", parameterName);
        }

        return normalizedValue;
    }
}
