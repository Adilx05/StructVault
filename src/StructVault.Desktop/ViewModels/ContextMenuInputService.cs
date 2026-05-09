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

    public bool ConfirmDelete(string title, string message)
    {
        MessageBoxResult result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }

    public void ShowValidationError(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
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
        Window? owner = global::System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        if (owner is not null && !ReferenceEquals(owner, dialog))
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true;
    }
}
