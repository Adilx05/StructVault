using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class DeleteVaultFieldCommandHandler : IRequestHandler<DeleteVaultFieldCommand>
{
    private readonly IVaultFieldWriter fieldWriter;

    public DeleteVaultFieldCommandHandler(IVaultFieldWriter fieldWriter)
    {
        this.fieldWriter = fieldWriter ?? throw new ArgumentNullException(nameof(fieldWriter));
    }

    public async Task Handle(DeleteVaultFieldCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await fieldWriter.DeleteAsync(request.Connection, request, cancellationToken).ConfigureAwait(false);
    }
}
