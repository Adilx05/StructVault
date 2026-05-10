using StructVault.Desktop.Services;

namespace StructVault.Architecture.Tests;

internal sealed class InMemoryApplicationSettingsService : IApplicationSettingsService
{
    public InMemoryApplicationSettingsService()
        : this(ApplicationSettings.Default)
    {
    }

    public InMemoryApplicationSettingsService(ApplicationSettings settings)
    {
        Settings = settings.Normalize();
    }

    public ApplicationSettings Settings { get; private set; }

    public ApplicationSettings Load()
    {
        return Settings;
    }

    public void Save(ApplicationSettings settings)
    {
        Settings = settings.Normalize();
    }
}
