using System.Globalization;
using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class SaveClipboardSettingsCommandHandler : IRequestHandler<SaveClipboardSettingsCommand>
{
    private readonly IVaultSettingWriter settingWriter;

    public SaveClipboardSettingsCommandHandler(IVaultSettingWriter settingWriter)
    {
        this.settingWriter = settingWriter ?? throw new ArgumentNullException(nameof(settingWriter));
    }

    public async Task Handle(SaveClipboardSettingsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        VaultSettingRecord[] settings =
        [
            new(VaultSettingNames.ClipboardAutoClearEnabled, request.AutoClearEnabled.ToString(CultureInfo.InvariantCulture)),
            new(VaultSettingNames.ClipboardAutoClearDelaySeconds, ((int)request.AutoClearDelay.TotalSeconds).ToString(CultureInfo.InvariantCulture))
        ];

        await settingWriter.UpsertManyAsync(request.Connection, settings, cancellationToken).ConfigureAwait(false);
    }
}
