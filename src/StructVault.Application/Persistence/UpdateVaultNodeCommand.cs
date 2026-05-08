using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Persistence;

public sealed class UpdateVaultNodeCommand : ICommand<bool>
{
    public UpdateVaultNodeCommand(
        DbConnection connection,
        string id,
        string name,
        int sortOrder,
        DateTimeOffset updatedAtUtc)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Id = RequireNonEmpty(id, nameof(id));
        Name = RequireNonEmpty(name, nameof(name));

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), sortOrder, "Vault node sort order cannot be negative.");
        }

        if (updatedAtUtc == default)
        {
            throw new ArgumentException("Vault node update timestamp must be specified.", nameof(updatedAtUtc));
        }

        SortOrder = sortOrder;
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }

    public DbConnection Connection { get; }

    public string Id { get; }

    public string Name { get; }

    public int SortOrder { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

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
