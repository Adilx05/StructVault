using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Persistence;

public sealed class UpdateVaultFieldCommand : ICommand<bool>
{
    private readonly byte[] value;

    public UpdateVaultFieldCommand(
        DbConnection connection,
        string id,
        string key,
        byte[] value,
        int sortOrder,
        DateTimeOffset updatedAtUtc)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Id = RequireNonEmpty(id, nameof(id));
        Key = RequireNonEmpty(key, nameof(key));
        this.value = RequireNonEmptyValue(value);

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), sortOrder, "Vault field sort order cannot be negative.");
        }

        if (updatedAtUtc == default)
        {
            throw new ArgumentException("Vault field update timestamp must be specified.", nameof(updatedAtUtc));
        }

        SortOrder = sortOrder;
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }

    public DbConnection Connection { get; }

    public string Id { get; }

    public string Key { get; }

    public byte[] Value => value.ToArray();

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

    private static byte[] RequireNonEmptyValue(byte[] value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value.Length == 0)
        {
            throw new ArgumentException("Vault field value cannot be empty.", nameof(value));
        }

        return value.ToArray();
    }
}
