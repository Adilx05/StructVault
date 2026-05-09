namespace StructVault.Desktop.ViewModels;

public interface IContextMenuInputService
{
    string? RequestNodeName(string title, string message, string? initialName = null);

    VaultFieldInput? RequestField(string title, string keyMessage, string valueMessage, VaultFieldInput? initialValue = null);

    bool ConfirmDelete(string title, string message);

    void ShowValidationError(string title, string message);
}
