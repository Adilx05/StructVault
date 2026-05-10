using System.IO;
using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class GetThemeSettingsQueryHandler : IRequestHandler<GetThemeSettingsQuery, ThemeSettingsRecord>
{
    private static readonly string[] ThemeSettingNames =
    [
        VaultSettingNames.ThemeName
    ];

    private readonly IVaultSettingReader settingReader;

    public GetThemeSettingsQueryHandler(IVaultSettingReader settingReader)
    {
        this.settingReader = settingReader ?? throw new ArgumentNullException(nameof(settingReader));
    }

    public async Task<ThemeSettingsRecord> Handle(GetThemeSettingsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<VaultSettingRecord> settings = await settingReader
            .ListByNamesAsync(request.Connection, ThemeSettingNames, cancellationToken)
            .ConfigureAwait(false);
        Dictionary<string, string> settingsByName = settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);

        string themeName = ReadThemeNameSetting(settingsByName, VaultSettingNames.ThemeName, ThemeSettingsRecord.Default.ThemeName);
        return new ThemeSettingsRecord(themeName);
    }

    private static string ReadThemeNameSetting(Dictionary<string, string> settingsByName, string name, string defaultValue)
    {
        if (!settingsByName.TryGetValue(name, out string? value))
        {
            return defaultValue;
        }

        try
        {
            return ThemeSettingsRecord.NormalizeThemeName(value);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            throw new InvalidDataException($"Vault setting '{name}' must be a supported theme name.", ex);
        }
    }
}
