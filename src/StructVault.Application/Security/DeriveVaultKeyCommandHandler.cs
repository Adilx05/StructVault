using MediatR;
using StructVault.Application.Abstractions.Security;

namespace StructVault.Application.Security;

public sealed class DeriveVaultKeyCommandHandler : IRequestHandler<DeriveVaultKeyCommand, byte[]>
{
    private const int MinimumSaltSizeInBytes = 16;
    private readonly IKeyDerivationService keyDerivationService;

    public DeriveVaultKeyCommandHandler(IKeyDerivationService keyDerivationService)
    {
        this.keyDerivationService = keyDerivationService ?? throw new ArgumentNullException(nameof(keyDerivationService));
    }

    public Task<byte[]> Handle(DeriveVaultKeyCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);

        return Task.FromResult(keyDerivationService.DeriveKey(request.Password, request.Salt));
    }

    private static void Validate(DeriveVaultKeyCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("A non-empty password is required.", nameof(request));
        }

        if (request.Salt.Length < MinimumSaltSizeInBytes)
        {
            throw new ArgumentException($"Salt must be at least {MinimumSaltSizeInBytes} bytes.", nameof(request));
        }
    }
}
