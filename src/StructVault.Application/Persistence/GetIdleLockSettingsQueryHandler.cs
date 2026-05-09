using System.Globalization;
using System.IO;
using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class GetIdleLockSettingsQueryHandler : IRequestHandler<GetIdleLockSettingsQuery, IdleLockSettingsRecord>
{
    private static readonly string[] IdleLockSettingNames =
    [
        VaultSettingNames.IdleLockEnabled,
        VaultSettingNames.IdleLockTimeoutSeconds
    ];

    private readonly IVaultSettingReader settingReader;

    public GetIdleLockSettingsQueryHandler(IVaultSettingReader settingReader)
    {
        this.settingReader = settingReader ?? throw new ArgumentNullException(nameof(settingReader));
    }

    public async Task<IdleLockSettingsRecord> Handle(GetIdleLockSettingsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<VaultSettingRecord> settings = await settingReader
            .ListByNamesAsync(request.Connection, IdleLockSettingNames, cancellationToken)
            .ConfigureAwait(false);
        Dictionary<string, string> settingsByName = settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);

        IdleLockSettingsRecord defaults = IdleLockSettingsRecord.Default;
        bool isEnabled = ReadBooleanSetting(settingsByName, VaultSettingNames.IdleLockEnabled, defaults.IsEnabled);
        TimeSpan timeout = ReadPositiveSecondsSetting(settingsByName, VaultSettingNames.IdleLockTimeoutSeconds, defaults.Timeout);

        return new IdleLockSettingsRecord(isEnabled, timeout);
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
