using ControlzEx.Theming;
using StructVault.Application.Persistence;

namespace StructVault.Desktop.Services;

public sealed class MahAppsThemeService : IThemeService
{
    public void ApplyTheme(string themeName)
    {
        string normalizedThemeName = ThemeSettingsRecord.NormalizeThemeName(themeName);
        global::System.Windows.Application? application = global::System.Windows.Application.Current; 
        if (application is null)
        {
            return;
        }

        ThemeManager.Current.ChangeTheme(application, normalizedThemeName);
    }
}
