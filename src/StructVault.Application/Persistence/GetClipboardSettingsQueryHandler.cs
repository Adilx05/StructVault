using System.Globalization;
using System.IO;
using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class GetClipboardSettingsQueryHandler : IRequestHandler<GetClipboardSettingsQuery, ClipboardSettingsRecord>
{
    private static readonly string[] ClipboardSettingNames =
    [
        VaultSettingNames.ClipboardAutoClearEnabled,
        VaultSettingNames.ClipboardAutoClearDelaySeconds
    ];

    private readonly IVaultSettingReader settingReader;

    public GetClipboardSettingsQueryHandler(IVaultSettingReader settingReader)
    {
        this.settingReader = settingReader ?? throw new ArgumentNullException(nameof(settingReader));
    }

    public async Task<ClipboardSettingsRecord> Handle(GetClipboardSettingsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<VaultSettingRecord> settings = await settingReader
            .ListByNamesAsync(request.Connection, ClipboardSettingNames, cancellationToken)
            .ConfigureAwait(false);
        Dictionary<string, string> settingsByName = settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);

        ClipboardSettingsRecord defaults = ClipboardSettingsRecord.Default;
        bool autoClearEnabled = ReadBooleanSetting(
            settingsByName,
            VaultSettingNames.ClipboardAutoClearEnabled,
            defaults.AutoClearEnabled);
        TimeSpan autoClearDelay = ReadPositiveSecondsSetting(
            settingsByName,
            VaultSettingNames.ClipboardAutoClearDelaySeconds,
            defaults.AutoClearDelay);

        return new ClipboardSettingsRecord(autoClearEnabled, autoClearDelay);
    }

    private static bool ReadBooleanSetting(Dictionary<string, string> settingsByName, string name, bool defaultValue)
    {
        if (!settingsByName.TryGetValue(name, out string? value))
        {
            return defaultValue;
        }

        if (bool.TryParse(value, out bool result))
        {
            return result;
        }

        throw new InvalidDataException($"Vault setting '{name}' must be a Boolean value.");
    }

    private static TimeSpan ReadPositiveSecondsSetting(Dictionary<string, string> settingsByName, string name, TimeSpan defaultValue)
    {
        if (!settingsByName.TryGetValue(name, out string? value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) || seconds <= 0)
        {
            throw new InvalidDataException($"Vault setting '{name}' must be a positive whole-second value.");
        }

        return TimeSpan.FromSeconds(seconds);
    }
}
