using System.Globalization;
using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class SaveIdleLockSettingsCommandHandler : IRequestHandler<SaveIdleLockSettingsCommand>
{
    private readonly IVaultSettingWriter settingWriter;

    public SaveIdleLockSettingsCommandHandler(IVaultSettingWriter settingWriter)
    {
        this.settingWriter = settingWriter ?? throw new ArgumentNullException(nameof(settingWriter));
    }

    public async Task Handle(SaveIdleLockSettingsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        VaultSettingRecord[] settings =
        [
            new(VaultSettingNames.IdleLockEnabled, request.IsEnabled.ToString(CultureInfo.InvariantCulture)),
            new(VaultSettingNames.IdleLockTimeoutSeconds, ((int)request.Timeout.TotalSeconds).ToString(CultureInfo.InvariantCulture))
        ];

        await settingWriter.UpsertManyAsync(request.Connection, settings, cancellationToken).ConfigureAwait(false);
    }
}
