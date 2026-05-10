namespace StructVault.Desktop.Services;

public interface IApplicationSettingsService
{
    ApplicationSettings Load();

    void Save(ApplicationSettings settings);
}
