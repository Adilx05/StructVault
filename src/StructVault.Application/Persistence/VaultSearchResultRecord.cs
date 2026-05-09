namespace StructVault.Application.Persistence;

public sealed class VaultSearchResultRecord
{
    public VaultSearchResultRecord(
        VaultSearchResultKind kind,
        string nodeId,
        string nodeName,
        string? fieldId,
        string? fieldKey,
        string matchedProperty)
    {
        if (!Enum.IsDefined(typeof(VaultSearchResultKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Vault search result kind is not supported.");
        }

        Kind = kind;
        NodeId = RequireNonEmpty(nodeId, nameof(nodeId));
        NodeName = RequireNonEmpty(nodeName, nameof(nodeName));
        FieldId = NormalizeOptional(fieldId);
        FieldKey = NormalizeOptional(fieldKey);
        MatchedProperty = RequireNonEmpty(matchedProperty, nameof(matchedProperty));

        if (Kind == VaultSearchResultKind.Node && (FieldId is not null || FieldKey is not null))
        {
            throw new ArgumentException("Node search results cannot include field metadata.", nameof(kind));
        }

        if (Kind == VaultSearchResultKind.Field && (FieldId is null || FieldKey is null))
        {
            throw new ArgumentException("Field search results require field metadata.", nameof(kind));
        }
    }

    public VaultSearchResultKind Kind { get; }

    public string NodeId { get; }

    public string NodeName { get; }

    public string? FieldId { get; }

    public string? FieldKey { get; }

    public string MatchedProperty { get; }

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
