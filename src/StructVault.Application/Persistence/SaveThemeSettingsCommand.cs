using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Persistence;

public sealed class SaveThemeSettingsCommand : ICommand
{
    public SaveThemeSettingsCommand(DbConnection connection, string themeName)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        ThemeName = ThemeSettingsRecord.NormalizeThemeName(themeName);
    }

    public DbConnection Connection { get; }

    public string ThemeName { get; }
}
