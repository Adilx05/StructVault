namespace StructVault.Application.Persistence;

public sealed class VaultNodeRecord
{
    public VaultNodeRecord(
        string id,
        string? parentNodeId,
        string name,
        int sortOrder,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = RequireNonEmpty(id, nameof(id));
        ParentNodeId = NormalizeOptional(parentNodeId);
        Name = RequireNonEmpty(name, nameof(name));

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), sortOrder, "Vault node sort order cannot be negative.");
        }

        if (createdAtUtc == default)
        {
            throw new ArgumentException("Vault node creation timestamp must be specified.", nameof(createdAtUtc));
        }

        if (updatedAtUtc == default)
        {
            throw new ArgumentException("Vault node update timestamp must be specified.", nameof(updatedAtUtc));
        }

        DateTimeOffset normalizedCreatedAtUtc = createdAtUtc.ToUniversalTime();
        DateTimeOffset normalizedUpdatedAtUtc = updatedAtUtc.ToUniversalTime();
        if (normalizedUpdatedAtUtc < normalizedCreatedAtUtc)
        {
            throw new ArgumentException("Vault node update timestamp cannot be earlier than its creation timestamp.", nameof(updatedAtUtc));
        }

        CreatedAtUtc = normalizedCreatedAtUtc;
        UpdatedAtUtc = normalizedUpdatedAtUtc;
        SortOrder = sortOrder;
    }

    public string Id { get; }

    public string? ParentNodeId { get; }

    public string Name { get; }

    public int SortOrder { get; }

    public DateTimeOffset CreatedAtUtc { get; }

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

    private static string? NormalizeOptional(string? value)
    {
        if (value is null)
        {
            return null;
        }

        string normalizedValue = value.Trim();
        return normalizedValue.Length == 0 ? null : normalizedValue;
    }
}
