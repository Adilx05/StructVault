using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using MediatR;
using StructVault.Application.Persistence;

namespace StructVault.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ISender sender;
    private readonly ObservableCollection<VaultTreeNodeViewModel> vaultNodes = new();
    private readonly ReadOnlyObservableCollection<VaultTreeNodeViewModel> readOnlyVaultNodes;

    public MainWindowViewModel(ISender sender)
    {
        this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
        readOnlyVaultNodes = new ReadOnlyObservableCollection<VaultTreeNodeViewModel>(vaultNodes);
    }

    public ReadOnlyObservableCollection<VaultTreeNodeViewModel> VaultNodes => readOnlyVaultNodes;

    public async Task LoadVaultTreeAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        RequireOpenConnection(connection);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<VaultNodeHierarchyRecord> hierarchy = await sender
            .Send(new ListVaultNodeHierarchyQuery(connection), cancellationToken)
            .ConfigureAwait(true);

        ReplaceVaultNodes(hierarchy);
    }

    private void ReplaceVaultNodes(IReadOnlyList<VaultNodeHierarchyRecord> hierarchy)
    {
        ArgumentNullException.ThrowIfNull(hierarchy);

        vaultNodes.Clear();
        foreach (VaultNodeHierarchyRecord node in hierarchy)
        {
            if (node is null)
            {
                throw new ArgumentException("Vault hierarchy cannot contain null root nodes.", nameof(hierarchy));
            }

            vaultNodes.Add(new VaultTreeNodeViewModel(node));
        }
    }

    private static void RequireOpenConnection(DbConnection connection)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Loading the vault tree requires an open vault database connection.");
        }
    }
}
