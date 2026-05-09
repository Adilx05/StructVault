using System.Text;
using MediatR;
using StructVault.Application.Abstractions.Clipboard;
using StructVault.Application.Abstractions.Persistence;
using StructVault.Application.Persistence;

namespace StructVault.Application.Clipboard;

public sealed class CopyVaultFieldValueToClipboardCommandHandler : IRequestHandler<CopyVaultFieldValueToClipboardCommand>
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private readonly IVaultFieldReader fieldReader;
    private readonly IClipboardService clipboardService;

    public CopyVaultFieldValueToClipboardCommandHandler(IVaultFieldReader fieldReader, IClipboardService clipboardService)
    {
        this.fieldReader = fieldReader ?? throw new ArgumentNullException(nameof(fieldReader));
        this.clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
    }

    public async Task Handle(CopyVaultFieldValueToClipboardCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        VaultFieldRecord? field = await fieldReader
            .GetByIdAsync(request.Connection, new GetVaultFieldByIdQuery(request.Connection, request.FieldId), cancellationToken)
            .ConfigureAwait(false);
        if (field is null)
        {
            throw new InvalidOperationException("Vault field could not be found for clipboard copy.");
        }

        string text = DecodeTextValue(field.Value);
        await clipboardService.SetTextAsync(text, cancellationToken).ConfigureAwait(false);
    }

    private static string DecodeTextValue(byte[] value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        try
        {
            return StrictUtf8.GetString(value);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidOperationException("Only UTF-8 text field values can be copied to the clipboard.", ex);
        }
    }
}
