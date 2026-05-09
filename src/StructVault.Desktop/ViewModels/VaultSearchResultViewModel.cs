using StructVault.Application.Persistence;

namespace StructVault.Desktop.ViewModels;

public sealed class VaultSearchResultViewModel : ViewModelBase
{
    public VaultSearchResultViewModel(VaultSearchResultRecord result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Kind = result.Kind;
        NodeId = RequireNonEmpty(result.NodeId, nameof(result));
        NodeName = RequireNonEmpty(result.NodeName, nameof(result));
        FieldId = NormalizeOptional(result.FieldId);
        FieldKey = NormalizeOptional(result.FieldKey);
        MatchedProperty = RequireNonEmpty(result.MatchedProperty, nameof(result));
        Title = CreateTitle();
        Subtitle = CreateSubtitle();
    }

    public VaultSearchResultKind Kind { get; }

    public string NodeId { get; }

    public string NodeName { get; }

    public string? FieldId { get; }

    public string? FieldKey { get; }

    public string MatchedProperty { get; }

    public string Title { get; }

    public string Subtitle { get; }

    private string CreateTitle()
    {
        return Kind == VaultSearchResultKind.Node ? NodeName : FieldKey!;
    }

    private string CreateSubtitle()
    {
        return Kind == VaultSearchResultKind.Node
            ? "Node name"
            : $"{MatchedProperty} in {NodeName}";
    }

    private static string RequireNonEmpty(string value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Vault search result values cannot be empty or whitespace.", parameterName);
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
