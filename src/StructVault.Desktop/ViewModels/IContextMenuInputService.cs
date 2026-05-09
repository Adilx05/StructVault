namespace StructVault.Desktop.ViewModels;

public interface IContextMenuInputService
{
    string? RequestNodeName(string title, string message, string? initialName = null);

    VaultFieldInput? RequestField(string title, string keyMessage, string valueMessage, VaultFieldInput? initialValue = null);

    string? RequestPassword(string title, string message);

    bool ConfirmDelete(string title, string message);

    UnsavedChangesExitChoice PromptUnsavedChangesOnExit(bool canSave);

    void ShowValidationError(string title, string message);
}
