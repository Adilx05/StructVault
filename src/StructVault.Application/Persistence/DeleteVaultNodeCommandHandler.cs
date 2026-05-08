using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class DeleteVaultNodeCommandHandler : IRequestHandler<DeleteVaultNodeCommand>
{
    private readonly IVaultNodeWriter nodeWriter;

    public DeleteVaultNodeCommandHandler(IVaultNodeWriter nodeWriter)
    {
        this.nodeWriter = nodeWriter ?? throw new ArgumentNullException(nameof(nodeWriter));
    }

    public async Task Handle(DeleteVaultNodeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await nodeWriter.DeleteAsync(request.Connection, request, cancellationToken).ConfigureAwait(false);
    }
}
