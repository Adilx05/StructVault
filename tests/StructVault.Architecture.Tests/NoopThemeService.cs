using StructVault.Desktop.Services;

namespace StructVault.Architecture.Tests;

internal sealed class NoopThemeService : IThemeService
{
    public void ApplyTheme(string themeName)
    {
    }
}
