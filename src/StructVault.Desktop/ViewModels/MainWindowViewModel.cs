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
    private readonly ObservableCollection<VaultFieldViewModel> selectedFields = new();
    private readonly ReadOnlyObservableCollection<VaultTreeNodeViewModel> readOnlyVaultNodes;
    private readonly ReadOnlyObservableCollection<VaultFieldViewModel> readOnlySelectedFields;
    private DbConnection? activeConnection;
    private VaultTreeNodeViewModel? selectedNode;

    public MainWindowViewModel(ISender sender)
    {
        this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
        readOnlyVaultNodes = new ReadOnlyObservableCollection<VaultTreeNodeViewModel>(vaultNodes);
        readOnlySelectedFields = new ReadOnlyObservableCollection<VaultFieldViewModel>(selectedFields);
    }

    public ReadOnlyObservableCollection<VaultTreeNodeViewModel> VaultNodes => readOnlyVaultNodes;

    public ReadOnlyObservableCollection<VaultFieldViewModel> SelectedFields => readOnlySelectedFields;

    public VaultTreeNodeViewModel? SelectedNode
    {
        get => selectedNode;
        private set
        {
            if (SetProperty(ref selectedNode, value))
            {
                OnPropertyChanged(nameof(SelectedNodeName));
                OnPropertyChanged(nameof(HasSelectedNode));
            }
        }
    }

    public string SelectedNodeName => SelectedNode?.Name ?? string.Empty;

    public bool HasSelectedNode => SelectedNode is not null;

    public bool HasSelectedFields => selectedFields.Count > 0;

    public async Task LoadVaultTreeAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        RequireOpenConnection(connection);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<VaultNodeHierarchyRecord> hierarchy = await sender
            .Send(new ListVaultNodeHierarchyQuery(connection), cancellationToken)
            .ConfigureAwait(true);

        activeConnection = connection;
        ReplaceVaultNodes(hierarchy);
        await SelectVaultNodeAsync(null, cancellationToken).ConfigureAwait(true);
    }

    public async Task SelectVaultNodeAsync(VaultTreeNodeViewModel? node, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (node is null)
        {
            SelectedNode = null;
            ClearSelectedFields();
            return;
        }

        DbConnection connection = RequireActiveOpenConnection();
        SelectedNode = node;
        ClearSelectedFields();

        IReadOnlyList<VaultFieldRecord> fields = await sender
            .Send(new ListVaultFieldsByNodeIdQuery(connection, node.Id), cancellationToken)
            .ConfigureAwait(true);

        ReplaceSelectedFields(fields);
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

    private void ReplaceSelectedFields(IReadOnlyList<VaultFieldRecord> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        ClearSelectedFields();
        foreach (VaultFieldRecord field in fields)
        {
            if (field is null)
            {
                throw new ArgumentException("Vault fields cannot contain null entries.", nameof(fields));
            }

            selectedFields.Add(new VaultFieldViewModel(field));
        }

        OnPropertyChanged(nameof(HasSelectedFields));
    }

    private void ClearSelectedFields()
    {
        selectedFields.Clear();
        OnPropertyChanged(nameof(HasSelectedFields));
    }

    private DbConnection RequireActiveOpenConnection()
    {
        if (activeConnection is null)
        {
            throw new InvalidOperationException("Selecting a vault node requires a loaded vault database connection.");
        }

        RequireOpenConnection(activeConnection);
        return activeConnection;
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
