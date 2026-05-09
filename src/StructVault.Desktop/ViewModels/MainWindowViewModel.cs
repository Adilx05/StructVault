using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Windows.Input;
using MediatR;
using StructVault.Application.Clipboard;
using StructVault.Application.Persistence;
using StructVault.Application.Qps;
using StructVault.Desktop.Commands;

namespace StructVault.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly IReadOnlyList<VaultSearchFilterOption> AvailableSearchFilterOptions =
    [
        new(SearchVaultFilter.All, "All"),
        new(SearchVaultFilter.Nodes, "Nodes"),
        new(SearchVaultFilter.Fields, "Fields")
    ];

    private readonly ISender sender;
    private readonly IContextMenuInputService contextMenuInputService;
    private readonly ObservableCollection<VaultTreeNodeViewModel> vaultNodes = new();
    private readonly ObservableCollection<VaultFieldViewModel> selectedFields = new();
    private readonly ObservableCollection<VaultSearchResultViewModel> searchResults = new();
    private readonly ReadOnlyObservableCollection<VaultTreeNodeViewModel> readOnlyVaultNodes;
    private readonly ReadOnlyObservableCollection<VaultFieldViewModel> readOnlySelectedFields;
    private readonly ReadOnlyObservableCollection<VaultSearchResultViewModel> readOnlySearchResults;
    private DbConnection? activeConnection;
    private string? activeVaultFilePath;
    private string? activeVaultPassword;
    private VaultTreeNodeViewModel? selectedNode;
    private string searchText = string.Empty;
    private SearchVaultFilter selectedSearchFilter = SearchVaultFilter.All;
    private bool isClipboardAutoClearEnabled = true;
    private TimeSpan clipboardAutoClearDelay = CopyVaultFieldValueToClipboardCommand.DefaultAutoClearDelay;
    private bool isDirty;

    public MainWindowViewModel(ISender sender)
        : this(sender, new ContextMenuInputService())
    {
    }

    public MainWindowViewModel(ISender sender, IContextMenuInputService contextMenuInputService)
    {
        this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
        this.contextMenuInputService = contextMenuInputService ?? throw new ArgumentNullException(nameof(contextMenuInputService));
        readOnlyVaultNodes = new ReadOnlyObservableCollection<VaultTreeNodeViewModel>(vaultNodes);
        readOnlySelectedFields = new ReadOnlyObservableCollection<VaultFieldViewModel>(selectedFields);
        readOnlySearchResults = new ReadOnlyObservableCollection<VaultSearchResultViewModel>(searchResults);

        SaveVaultCommand = new AsyncCommand((parameter, cancellationToken) => SaveVaultAsync(parameter, cancellationToken), CanSaveVault);
        SearchVaultCommand = new AsyncCommand((parameter, cancellationToken) => SearchVaultAsync(cancellationToken), CanSearchVault);
        AddRootNodeCommand = new AsyncCommand(AddRootNodeAsync, CanMutateVault);
        AddChildNodeCommand = new AsyncCommand(AddChildNodeAsync, CanMutateNode);
        RenameNodeCommand = new AsyncCommand(RenameNodeAsync, CanMutateNode);
        DeleteNodeCommand = new AsyncCommand(DeleteNodeAsync, CanMutateNode);
        AddFieldCommand = new AsyncCommand(AddFieldAsync, CanMutateNode);
        EditFieldCommand = new AsyncCommand(EditFieldAsync, CanMutateField);
        DeleteFieldCommand = new AsyncCommand(DeleteFieldAsync, CanMutateField);
        CopyFieldValueCommand = new AsyncCommand(CopyFieldValueAsync, CanMutateField);
    }

    public ReadOnlyObservableCollection<VaultTreeNodeViewModel> VaultNodes => readOnlyVaultNodes;

    public ReadOnlyObservableCollection<VaultFieldViewModel> SelectedFields => readOnlySelectedFields;

    public ReadOnlyObservableCollection<VaultSearchResultViewModel> SearchResults => readOnlySearchResults;

    public IReadOnlyList<VaultSearchFilterOption> SearchFilterOptions => AvailableSearchFilterOptions;

    public ICommand SaveVaultCommand { get; }

    public ICommand SearchVaultCommand { get; }

    public ICommand AddRootNodeCommand { get; }

    public ICommand AddChildNodeCommand { get; }

    public ICommand RenameNodeCommand { get; }

    public ICommand DeleteNodeCommand { get; }

    public ICommand AddFieldCommand { get; }

    public ICommand EditFieldCommand { get; }

    public ICommand DeleteFieldCommand { get; }

    public ICommand CopyFieldValueCommand { get; }

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

    public bool HasSearchResults => searchResults.Count > 0;

    public bool HasSearchText => SearchText.Length > 0;

    public bool IsClipboardAutoClearEnabled
    {
        get => isClipboardAutoClearEnabled;
        set => SetProperty(ref isClipboardAutoClearEnabled, value);
    }

    public TimeSpan ClipboardAutoClearDelay
    {
        get => clipboardAutoClearDelay;
        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Clipboard auto-clear delay must be greater than zero.");
            }

            SetProperty(ref clipboardAutoClearDelay, value);
        }
    }

    public SearchVaultFilter SelectedSearchFilter
    {
        get => selectedSearchFilter;
        set
        {
            if (!Enum.IsDefined(typeof(SearchVaultFilter), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Vault search filter is not supported.");
            }

            if (SetProperty(ref selectedSearchFilter, value) && HasSearchText && activeConnection?.State == ConnectionState.Open)
            {
                ClearSearchResults();
            }
        }
    }

    public string SearchStatusText => HasSearchText
        ? $"{searchResults.Count} search result{(searchResults.Count == 1 ? string.Empty : "s")}"
        : "Enter text to search nodes and fields.";

    public string SearchText
    {
        get => searchText;
        set
        {
            string normalizedValue = value?.Trim() ?? string.Empty;
            if (SetProperty(ref searchText, normalizedValue))
            {
                if (searchText.Length == 0)
                {
                    ClearSearchResults();
                }

                OnPropertyChanged(nameof(HasSearchText));
                OnPropertyChanged(nameof(SearchStatusText));
            }
        }
    }

    public bool IsDirty
    {
        get => isDirty;
        private set => SetProperty(ref isDirty, value);
    }

    public bool CanSave => CanSaveVault(null);

    public async Task<bool> ConfirmExitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsDirty)
        {
            return true;
        }

        UnsavedChangesExitChoice choice = contextMenuInputService.PromptUnsavedChangesOnExit(CanSave);
        return choice switch
        {
            UnsavedChangesExitChoice.SaveAndExit => await SaveAndConfirmExitAsync(cancellationToken).ConfigureAwait(true),
            UnsavedChangesExitChoice.ExitWithoutSaving => true,
            UnsavedChangesExitChoice.CancelExit => false,
            _ => throw new InvalidOperationException($"Unsupported unsaved changes exit choice '{choice}'.")
        };
    }

    public async Task LoadVaultTreeAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        RequireOpenConnection(connection);
        cancellationToken.ThrowIfCancellationRequested();

        activeConnection = connection;
        await RefreshVaultTreeAsync(null, cancellationToken).ConfigureAwait(true);
        SearchText = string.Empty;
        SelectedSearchFilter = SearchVaultFilter.All;
        ClearSearchResults();
        SetClean();
        OnPropertyChanged(nameof(CanSave));
    }

    public async Task LoadVaultTreeAsync(DbConnection connection, string vaultFilePath, string password, CancellationToken cancellationToken = default)
    {
        ConfigureManualSaveTarget(vaultFilePath, password);
        await LoadVaultTreeAsync(connection, cancellationToken).ConfigureAwait(true);
    }

    public void ConfigureManualSaveTarget(string vaultFilePath, string password)
    {
        if (string.IsNullOrWhiteSpace(vaultFilePath))
        {
            throw new ArgumentException("A QPS vault file path is required for manual save.", nameof(vaultFilePath));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("A non-empty password is required for manual save.", nameof(password));
        }

        activeVaultFilePath = vaultFilePath;
        activeVaultPassword = password;
        OnPropertyChanged(nameof(CanSave));
    }

    public async Task SaveVaultAsync(CancellationToken cancellationToken = default)
    {
        await SaveVaultAsync(null, cancellationToken).ConfigureAwait(true);
    }

    public async Task SearchVaultAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DbConnection connection = RequireActiveOpenConnection();
        string normalizedSearchText = SearchText.Trim();
        if (normalizedSearchText.Length == 0)
        {
            ClearSearchResults();
            return;
        }

        IReadOnlyList<VaultSearchResultRecord> results = await sender
            .Send(new SearchVaultQuery(connection, normalizedSearchText, SelectedSearchFilter), cancellationToken)
            .ConfigureAwait(true);

        ReplaceSearchResults(results);
    }

    public async Task SelectSearchResultAsync(VaultSearchResultViewModel? result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (result is null)
        {
            return;
        }

        VaultTreeNodeViewModel? node = FindNodeById(vaultNodes, result.NodeId);
        if (node is null)
        {
            await RefreshVaultTreeAsync(result.NodeId, cancellationToken).ConfigureAwait(true);
            return;
        }

        await SelectVaultNodeAsync(node, cancellationToken).ConfigureAwait(true);
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

    private async Task<bool> SaveAndConfirmExitAsync(CancellationToken cancellationToken)
    {
        if (!CanSave)
        {
            return false;
        }

        try
        {
            await SaveVaultAsync(null, cancellationToken).ConfigureAwait(true);
            return !IsDirty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            contextMenuInputService.ShowValidationError("Save failed", "The vault could not be saved, so StructVault will remain open. " + ex.Message);
            return false;
        }
    }

    private async Task SaveVaultAsync(object? parameter, CancellationToken cancellationToken)
    {
        DbConnection connection = RequireActiveOpenConnection();
        if (string.IsNullOrWhiteSpace(activeVaultFilePath) || string.IsNullOrWhiteSpace(activeVaultPassword))
        {
            throw new InvalidOperationException("A vault file path and password must be configured before manual save.");
        }

        await sender.Send(
            new SaveQpsVaultFileCommand(connection, activeVaultFilePath, activeVaultPassword),
            cancellationToken).ConfigureAwait(true);
        SetClean();
    }

    private async Task AddRootNodeAsync(object? parameter, CancellationToken cancellationToken)
    {
        DbConnection connection = RequireActiveOpenConnection();
        string? requestedName = contextMenuInputService.RequestNodeName("Add root node", "Enter a name for the new root node.");
        string? name = NormalizeRequiredUserText(requestedName, "Node name", "Node names cannot be empty.");
        if (name is null)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string nodeId = CreateEntityId();
        int sortOrder = GetNextRootNodeSortOrder();
        await sender.Send(new CreateVaultNodeCommand(connection, nodeId, null, name, sortOrder, now, now), cancellationToken).ConfigureAwait(true);
        MarkDirty();
        await RefreshVaultTreeAsync(nodeId, cancellationToken).ConfigureAwait(true);
    }

    private async Task AddChildNodeAsync(object? parameter, CancellationToken cancellationToken)
    {
        VaultTreeNodeViewModel parentNode = RequireNodeParameter(parameter);
        DbConnection connection = RequireActiveOpenConnection();
        string? requestedName = contextMenuInputService.RequestNodeName("Add child node", $"Enter a name for the new child under '{parentNode.Name}'.");
        string? name = NormalizeRequiredUserText(requestedName, "Node name", "Node names cannot be empty.");
        if (name is null)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string nodeId = CreateEntityId();
        int sortOrder = parentNode.Children.Count == 0 ? 0 : parentNode.Children.Max(child => child.SortOrder) + 1;
        await sender.Send(new CreateVaultNodeCommand(connection, nodeId, parentNode.Id, name, sortOrder, now, now), cancellationToken).ConfigureAwait(true);
        MarkDirty();
        await RefreshVaultTreeAsync(nodeId, cancellationToken).ConfigureAwait(true);
    }

    private async Task RenameNodeAsync(object? parameter, CancellationToken cancellationToken)
    {
        VaultTreeNodeViewModel node = RequireNodeParameter(parameter);
        DbConnection connection = RequireActiveOpenConnection();
        string? requestedName = contextMenuInputService.RequestNodeName("Rename node", $"Enter a new name for '{node.Name}'.", node.Name);
        string? name = NormalizeRequiredUserText(requestedName, "Node name", "Node names cannot be empty.");
        if (name is null)
        {
            return;
        }

        bool updated = await sender.Send(new UpdateVaultNodeCommand(connection, node.Id, name, node.SortOrder, DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(true);
        if (updated)
        {
            MarkDirty();
            await RefreshVaultTreeAsync(node.Id, cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task DeleteNodeAsync(object? parameter, CancellationToken cancellationToken)
    {
        VaultTreeNodeViewModel node = RequireNodeParameter(parameter);
        DbConnection connection = RequireActiveOpenConnection();
        if (!contextMenuInputService.ConfirmDelete("Delete node", $"Delete '{node.Name}' and all nested nodes and fields?"))
        {
            return;
        }

        await sender.Send(new DeleteVaultNodeCommand(connection, node.Id), cancellationToken).ConfigureAwait(true);
        MarkDirty();
        await RefreshVaultTreeAsync(null, cancellationToken).ConfigureAwait(true);
    }

    private async Task AddFieldAsync(object? parameter, CancellationToken cancellationToken)
    {
        VaultTreeNodeViewModel node = RequireNodeParameter(parameter);
        DbConnection connection = RequireActiveOpenConnection();
        VaultFieldInput? requestedField = contextMenuInputService.RequestField(
            "Add field",
            "Enter the field key.",
            "Enter the field value.");
        VaultFieldInput? field = NormalizeRequiredFieldInput(requestedField);
        if (field is null)
        {
            return;
        }

        IReadOnlyList<VaultFieldRecord> existingFields = await sender
            .Send(new ListVaultFieldsByNodeIdQuery(connection, node.Id), cancellationToken)
            .ConfigureAwait(true);
        int sortOrder = existingFields.Count == 0 ? 0 : existingFields.Max(existingField => existingField.SortOrder) + 1;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await sender.Send(
            new CreateVaultFieldCommand(connection, CreateEntityId(), node.Id, field.Key, StrictUtf8.GetBytes(field.Value), sortOrder, now, now),
            cancellationToken).ConfigureAwait(true);
        MarkDirty();
        await SelectVaultNodeAsync(node, cancellationToken).ConfigureAwait(true);
    }

    private async Task EditFieldAsync(object? parameter, CancellationToken cancellationToken)
    {
        VaultFieldViewModel field = RequireFieldParameter(parameter);
        DbConnection connection = RequireActiveOpenConnection();
        VaultFieldInput? requestedField = contextMenuInputService.RequestField(
            "Edit field",
            "Enter the field key.",
            "Enter the field value.",
            new VaultFieldInput(field.Key, field.DisplayValue));
        VaultFieldInput? normalizedField = NormalizeRequiredFieldInput(requestedField);
        if (normalizedField is null)
        {
            return;
        }

        bool updated = await sender.Send(
            new UpdateVaultFieldCommand(connection, field.Id, normalizedField.Key, StrictUtf8.GetBytes(normalizedField.Value), field.SortOrder, DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(true);
        if (updated && SelectedNode is not null)
        {
            MarkDirty();
            await SelectVaultNodeAsync(SelectedNode, cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task CopyFieldValueAsync(object? parameter, CancellationToken cancellationToken)
    {
        VaultFieldViewModel field = RequireFieldParameter(parameter);
        DbConnection connection = RequireActiveOpenConnection();

        try
        {
            await sender.Send(
                new CopyVaultFieldValueToClipboardCommand(
                    connection,
                    field.Id,
                    IsClipboardAutoClearEnabled,
                    ClipboardAutoClearDelay),
                cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            contextMenuInputService.ShowValidationError("Copy failed", "The field value could not be copied. " + ex.Message);
        }
    }

    private async Task DeleteFieldAsync(object? parameter, CancellationToken cancellationToken)
    {
        VaultFieldViewModel field = RequireFieldParameter(parameter);
        DbConnection connection = RequireActiveOpenConnection();
        if (!contextMenuInputService.ConfirmDelete("Delete field", $"Delete field '{field.Key}'?"))
        {
            return;
        }

        await sender.Send(new DeleteVaultFieldCommand(connection, field.Id), cancellationToken).ConfigureAwait(true);
        MarkDirty();
        if (SelectedNode is not null)
        {
            await SelectVaultNodeAsync(SelectedNode, cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task RefreshVaultTreeAsync(string? nodeIdToSelect, CancellationToken cancellationToken)
    {
        DbConnection connection = RequireActiveOpenConnection();
        IReadOnlyList<VaultNodeHierarchyRecord> hierarchy = await sender
            .Send(new ListVaultNodeHierarchyQuery(connection), cancellationToken)
            .ConfigureAwait(true);

        ReplaceVaultNodes(hierarchy);
        VaultTreeNodeViewModel? nodeToSelect = nodeIdToSelect is null ? null : FindNodeById(vaultNodes, nodeIdToSelect);
        await SelectVaultNodeAsync(nodeToSelect, cancellationToken).ConfigureAwait(true);
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

    private void ReplaceSearchResults(IReadOnlyList<VaultSearchResultRecord> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        searchResults.Clear();
        foreach (VaultSearchResultRecord result in results)
        {
            if (result is null)
            {
                throw new ArgumentException("Vault search results cannot contain null entries.", nameof(results));
            }

            searchResults.Add(new VaultSearchResultViewModel(result));
        }

        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(SearchStatusText));
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

    private void ClearSearchResults()
    {
        searchResults.Clear();
        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(SearchStatusText));
    }

    private void MarkDirty()
    {
        IsDirty = true;
    }

    private void SetClean()
    {
        IsDirty = false;
    }

    private bool CanMutateVault(object? parameter)
    {
        return activeConnection?.State == ConnectionState.Open;
    }

    private bool CanSaveVault(object? parameter)
    {
        return CanMutateVault(parameter) &&
            !string.IsNullOrWhiteSpace(activeVaultFilePath) &&
            !string.IsNullOrWhiteSpace(activeVaultPassword);
    }

    private bool CanSearchVault(object? parameter)
    {
        return CanMutateVault(parameter) && !string.IsNullOrWhiteSpace(SearchText);
    }

    private bool CanMutateNode(object? parameter)
    {
        return parameter is VaultTreeNodeViewModel && CanMutateVault(parameter);
    }

    private bool CanMutateField(object? parameter)
    {
        return parameter is VaultFieldViewModel && CanMutateVault(parameter);
    }

    private int GetNextRootNodeSortOrder()
    {
        return vaultNodes.Count == 0 ? 0 : vaultNodes.Max(node => node.SortOrder) + 1;
    }

    private DbConnection RequireActiveOpenConnection()
    {
        if (activeConnection is null)
        {
            throw new InvalidOperationException("A loaded vault database connection is required for this operation.");
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

    private static VaultTreeNodeViewModel RequireNodeParameter(object? parameter)
    {
        if (parameter is not VaultTreeNodeViewModel node)
        {
            throw new ArgumentException("A vault node context menu command requires a vault node parameter.", nameof(parameter));
        }

        return node;
    }

    private static VaultFieldViewModel RequireFieldParameter(object? parameter)
    {
        if (parameter is not VaultFieldViewModel field)
        {
            throw new ArgumentException("A vault field context menu command requires a vault field parameter.", nameof(parameter));
        }

        return field;
    }

    private string? NormalizeRequiredUserText(string? value, string title, string emptyMessage)
    {
        if (value is null)
        {
            return null;
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            contextMenuInputService.ShowValidationError(title, emptyMessage);
            return null;
        }

        return normalizedValue;
    }

    private VaultFieldInput? NormalizeRequiredFieldInput(VaultFieldInput? value)
    {
        if (value is null)
        {
            return null;
        }

        string? key = NormalizeRequiredUserText(value.Key, "Field key", "Field keys cannot be empty.");
        if (key is null)
        {
            return null;
        }

        string? fieldValue = NormalizeRequiredUserText(value.Value, "Field value", "Field values cannot be empty.");
        if (fieldValue is null)
        {
            return null;
        }

        return new VaultFieldInput(key, fieldValue);
    }

    private static string CreateEntityId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static VaultTreeNodeViewModel? FindNodeById(IEnumerable<VaultTreeNodeViewModel> nodes, string id)
    {
        foreach (VaultTreeNodeViewModel node in nodes)
        {
            if (string.Equals(node.Id, id, StringComparison.Ordinal))
            {
                return node;
            }

            VaultTreeNodeViewModel? childMatch = FindNodeById(node.Children, id);
            if (childMatch is not null)
            {
                return childMatch;
            }
        }

        return null;
    }
}
