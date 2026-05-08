using MediatR;
using StructVault.Application.Abstractions.Security;

namespace StructVault.Application.Security;

public sealed class DecryptVaultDataCommandHandler : IRequestHandler<DecryptVaultDataCommand, byte[]>
{
    private const int Aes256KeySizeInBytes = 32;
    private const int NonceSizeInBytes = 12;
    private const int AuthenticationTagSizeInBytes = 16;
    private readonly IAuthenticatedEncryptionService encryptionService;

    public DecryptVaultDataCommandHandler(IAuthenticatedEncryptionService encryptionService)
    {
        this.encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
    }

    public Task<byte[]> Handle(DecryptVaultDataCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);

        return Task.FromResult(encryptionService.Decrypt(request.Ciphertext, request.Key, request.Nonce, request.Tag));
    }

    private static void Validate(DecryptVaultDataCommand request)
    {
        if (request.Key.Length != Aes256KeySizeInBytes)
        {
            throw new ArgumentException($"AES-256-GCM requires a {Aes256KeySizeInBytes}-byte key.", nameof(request));
        }

        if (request.Nonce.Length != NonceSizeInBytes)
        {
            throw new ArgumentException($"AES-GCM requires a {NonceSizeInBytes}-byte nonce.", nameof(request));
        }

        if (request.Tag.Length != AuthenticationTagSizeInBytes)
        {
            throw new ArgumentException($"AES-GCM requires a {AuthenticationTagSizeInBytes}-byte authentication tag.", nameof(request));
        }
    }
}
