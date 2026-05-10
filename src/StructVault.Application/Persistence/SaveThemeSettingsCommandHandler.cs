using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class SaveThemeSettingsCommandHandler : IRequestHandler<SaveThemeSettingsCommand>
{
    private readonly IVaultSettingWriter settingWriter;

    public SaveThemeSettingsCommandHandler(IVaultSettingWriter settingWriter)
    {
        this.settingWriter = settingWriter ?? throw new ArgumentNullException(nameof(settingWriter));
    }

    public async Task Handle(SaveThemeSettingsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        VaultSettingRecord[] settings =
        [
            new(VaultSettingNames.ThemeName, request.ThemeName)
        ];

        await settingWriter.UpsertManyAsync(request.Connection, settings, cancellationToken).ConfigureAwait(false);
    }
}
