using MediatR;
using StructVault.Application.Abstractions.Security;

namespace StructVault.Application.Security;

public sealed class EncryptVaultDataCommandHandler : IRequestHandler<EncryptVaultDataCommand, AesGcmEncryptionResult>
{
    private const int Aes256KeySizeInBytes = 32;
    private readonly IAuthenticatedEncryptionService encryptionService;

    public EncryptVaultDataCommandHandler(IAuthenticatedEncryptionService encryptionService)
    {
        this.encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
    }

    public Task<AesGcmEncryptionResult> Handle(EncryptVaultDataCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);

        return Task.FromResult(encryptionService.Encrypt(request.Plaintext, request.Key));
    }

    private static void Validate(EncryptVaultDataCommand request)
    {
        if (request.Key.Length != Aes256KeySizeInBytes)
        {
            throw new ArgumentException($"AES-256-GCM requires a {Aes256KeySizeInBytes}-byte key.", nameof(request));
        }
    }
}
