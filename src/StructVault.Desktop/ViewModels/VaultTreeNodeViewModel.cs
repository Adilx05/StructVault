using System.Collections.ObjectModel;
using StructVault.Application.Persistence;

namespace StructVault.Desktop.ViewModels;

public sealed class VaultTreeNodeViewModel : ViewModelBase
{
    private readonly ReadOnlyObservableCollection<VaultTreeNodeViewModel> children;

    public VaultTreeNodeViewModel(VaultNodeHierarchyRecord node)
    {
        ArgumentNullException.ThrowIfNull(node);

        Id = RequireNonEmpty(node.Id, nameof(node));
        ParentNodeId = NormalizeOptional(node.ParentNodeId);
        Name = RequireNonEmpty(node.Name, nameof(node));
        SortOrder = node.SortOrder;

        ObservableCollection<VaultTreeNodeViewModel> childNodes = new();
        foreach (VaultNodeHierarchyRecord child in node.Children)
        {
            if (child is null)
            {
                throw new ArgumentException("Vault tree node children cannot contain null entries.", nameof(node));
            }

            childNodes.Add(new VaultTreeNodeViewModel(child));
        }

        children = new ReadOnlyObservableCollection<VaultTreeNodeViewModel>(childNodes);
    }

    public string Id { get; }

    public string? ParentNodeId { get; }

    public string Name { get; }

    public int SortOrder { get; }

    public ReadOnlyObservableCollection<VaultTreeNodeViewModel> Children => children;

    private static string RequireNonEmpty(string value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Vault tree node values cannot be empty or whitespace.", parameterName);
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
