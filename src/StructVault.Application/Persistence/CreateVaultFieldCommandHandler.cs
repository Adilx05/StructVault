using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class CreateVaultFieldCommandHandler : IRequestHandler<CreateVaultFieldCommand>
{
    private readonly IVaultFieldWriter fieldWriter;

    public CreateVaultFieldCommandHandler(IVaultFieldWriter fieldWriter)
    {
        this.fieldWriter = fieldWriter ?? throw new ArgumentNullException(nameof(fieldWriter));
    }

    public async Task Handle(CreateVaultFieldCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await fieldWriter.CreateAsync(request.Connection, request, cancellationToken).ConfigureAwait(false);
    }
}
