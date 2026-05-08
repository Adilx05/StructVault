namespace StructVault.Application.Persistence;

public sealed class VaultNodeHierarchyRecord
{
    private readonly IReadOnlyList<VaultNodeHierarchyRecord> children;

    public VaultNodeHierarchyRecord(
        string id,
        string? parentNodeId,
        string name,
        int sortOrder,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        IReadOnlyList<VaultNodeHierarchyRecord> children)
    {
        Id = RequireNonEmpty(id, nameof(id));
        ParentNodeId = NormalizeOptional(parentNodeId);
        Name = RequireNonEmpty(name, nameof(name));

        if (ParentNodeId is not null && string.Equals(ParentNodeId, Id, StringComparison.Ordinal))
        {
            throw new ArgumentException("A vault node cannot be its own parent.", nameof(parentNodeId));
        }

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

        ArgumentNullException.ThrowIfNull(children);
        VaultNodeHierarchyRecord[] childSnapshot = children.ToArray();
        if (childSnapshot.Any(child => child is null))
        {
            throw new ArgumentException("Vault node hierarchy children cannot contain null entries.", nameof(children));
        }

        VaultNodeHierarchyRecord? invalidChild = childSnapshot.FirstOrDefault(child =>
            !string.Equals(child.ParentNodeId, Id, StringComparison.Ordinal));
        if (invalidChild is not null)
        {
            throw new ArgumentException("Vault node hierarchy children must reference their containing parent node.", nameof(children));
        }

        SortOrder = sortOrder;
        CreatedAtUtc = normalizedCreatedAtUtc;
        UpdatedAtUtc = normalizedUpdatedAtUtc;
        this.children = Array.AsReadOnly(childSnapshot);
    }

    public string Id { get; }

    public string? ParentNodeId { get; }

    public string Name { get; }

    public int SortOrder { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public IReadOnlyList<VaultNodeHierarchyRecord> Children => children;

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
