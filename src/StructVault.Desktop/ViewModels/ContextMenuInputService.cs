using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;

namespace StructVault.Desktop.ViewModels;

public sealed class ContextMenuInputService : IContextMenuInputService
{
    public string? RequestNodeName(string title, string message, string? initialName = null)
    {
        TextBox nameTextBox = CreateTextBox(initialName);
        bool accepted = ShowInputDialog(title, message, nameTextBox);
        return accepted ? nameTextBox.Text : null;
    }

    public VaultFieldInput? RequestField(string title, string keyMessage, string valueMessage, VaultFieldInput? initialValue = null)
    {
        TextBox keyTextBox = CreateTextBox(initialValue?.Key);
        TextBox valueTextBox = CreateTextBox(initialValue?.Value);
        StackPanel content = new() { Margin = new Thickness(0, 4, 0, 0) };
        content.Children.Add(new TextBlock { Text = keyMessage, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(keyTextBox);
        content.Children.Add(new TextBlock { Margin = new Thickness(0, 12, 0, 0), Text = valueMessage, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(valueTextBox);

        bool accepted = ShowInputDialog(title, null, content);
        return accepted ? new VaultFieldInput(keyTextBox.Text, valueTextBox.Text) : null;
    }

    public string? RequestPassword(string title, string message)
    {
        PasswordBox passwordBox = new()
        {
            Margin = new Thickness(0, 6, 0, 0),
            MinWidth = 320
        };

        bool accepted = ShowInputDialog(title, message, passwordBox);
        return accepted ? passwordBox.Password : null;
    }

    public bool ConfirmDelete(string title, string message)
    {
        string choice = ShowMahAppsChoiceDialog(
            title,
            message,
            [
                new DialogChoice("Yes", "yes", IsDefault: false),
                new DialogChoice("No", "no", IsCancel: true, IsDefault: false)
            ],
            "no");

        return string.Equals(choice, "yes", StringComparison.Ordinal);
    }

    public UnsavedChangesExitChoice PromptUnsavedChangesOnExit(bool canSave)
    {
        if (canSave)
        {
            string choice = ShowMahAppsChoiceDialog(
                "Unsaved changes",
                "The vault has unsaved changes. Save before exiting?",
                [
                    new DialogChoice("Save", "save", IsDefault: true),
                    new DialogChoice("Don't save", "discard"),
                    new DialogChoice("Cancel", "cancel", IsCancel: true)
                ],
                "cancel");

            return choice switch
            {
                "save" => UnsavedChangesExitChoice.SaveAndExit,
                "discard" => UnsavedChangesExitChoice.ExitWithoutSaving,
                _ => UnsavedChangesExitChoice.CancelExit
            };
        }

        string discardChoice = ShowMahAppsChoiceDialog(
            "Unsaved changes",
            "The vault has unsaved changes, but no save target is configured. Exit without saving?",
            [
                new DialogChoice("Exit without saving", "discard"),
                new DialogChoice("Cancel", "cancel", IsDefault: true, IsCancel: true)
            ],
            "cancel");

        return string.Equals(discardChoice, "discard", StringComparison.Ordinal)
            ? UnsavedChangesExitChoice.ExitWithoutSaving
            : UnsavedChangesExitChoice.CancelExit;
    }

    public void ShowValidationError(string title, string message)
    {
        _ = ShowMahAppsChoiceDialog(
            title,
            message,
            [new DialogChoice("OK", "ok", IsDefault: true, IsCancel: true)],
            "ok");
    }


    private static string ShowMahAppsChoiceDialog(string title, string message, IReadOnlyList<DialogChoice> choices, string defaultResult)
    {
        MetroWindow dialog = new()
        {
            Title = title,
            TitleCharacterCasing = CharacterCasing.Normal,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 420
        };

        string selectedResult = defaultResult;
        StackPanel layout = new() { Margin = new Thickness(18) };
        layout.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 520
        });

        StackPanel buttons = new()
        {
            Margin = new Thickness(0, 18, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Orientation = Orientation.Horizontal
        };

        foreach (DialogChoice choice in choices)
        {
            Button button = new()
            {
                Margin = buttons.Children.Count == 0 ? new Thickness(0) : new Thickness(8, 0, 0, 0),
                MinWidth = 84,
                Content = choice.Caption,
                IsDefault = choice.IsDefault,
                IsCancel = choice.IsCancel
            };
            button.Click += (_, _) =>
            {
                selectedResult = choice.Result;
                dialog.DialogResult = true;
            };
            buttons.Children.Add(button);
        }

        layout.Children.Add(buttons);
        dialog.Content = layout;
        Window? owner = GetActiveOwner(dialog);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        _ = dialog.ShowDialog();
        return selectedResult;
    }

    private static Window? GetActiveOwner(Window dialog)
    {
        return global::System.Windows.Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive && !ReferenceEquals(window, dialog));
    }

    private static TextBox CreateTextBox(string? value)
    {
        return new TextBox
        {
            Margin = new Thickness(0, 6, 0, 0),
            MinWidth = 320,
            Text = value ?? string.Empty
        };
    }

    private static bool ShowInputDialog(string title, string? message, FrameworkElement input)
    {
        MetroWindow dialog = new()
        {
            Title = title,
            TitleCharacterCasing = CharacterCasing.Normal,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 420
        };

        StackPanel layout = new() { Margin = new Thickness(18) };
        if (!string.IsNullOrWhiteSpace(message))
        {
            layout.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        }

        layout.Children.Add(input);

        StackPanel buttons = new()
        {
            Margin = new Thickness(0, 18, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Orientation = Orientation.Horizontal
        };
        Button cancelButton = new() { MinWidth = 84, Content = "Cancel", IsCancel = true };
        Button okButton = new() { Margin = new Thickness(8, 0, 0, 0), MinWidth = 84, Content = "OK", IsDefault = true };
        okButton.Click += (_, _) => dialog.DialogResult = true;
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(okButton);
        layout.Children.Add(buttons);

        dialog.Content = layout;
        Window? owner = GetActiveOwner(dialog);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true;
    }
}

internal sealed record DialogChoice(string Caption, string Result, bool IsDefault = false, bool IsCancel = false);
