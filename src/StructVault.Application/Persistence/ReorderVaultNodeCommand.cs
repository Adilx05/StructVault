using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Persistence;

public sealed class ReorderVaultNodeCommand : ICommand<bool>
{
    public ReorderVaultNodeCommand(
        DbConnection connection,
        string id,
        int targetSortOrder,
        DateTimeOffset updatedAtUtc)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Id = RequireNonEmpty(id, nameof(id));

        if (targetSortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetSortOrder), targetSortOrder, "Vault node target sort order cannot be negative.");
        }

        if (updatedAtUtc == default)
        {
            throw new ArgumentException("Vault node update timestamp must be specified.", nameof(updatedAtUtc));
        }

        TargetSortOrder = targetSortOrder;
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }

    public DbConnection Connection { get; }

    public string Id { get; }

    public int TargetSortOrder { get; }

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
