using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class ReorderVaultNodeCommandHandler : IRequestHandler<ReorderVaultNodeCommand, bool>
{
    private readonly IVaultNodeWriter nodeWriter;

    public ReorderVaultNodeCommandHandler(IVaultNodeWriter nodeWriter)
    {
        this.nodeWriter = nodeWriter ?? throw new ArgumentNullException(nameof(nodeWriter));
    }

    public async Task<bool> Handle(ReorderVaultNodeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return await nodeWriter.ReorderAsync(request.Connection, request, cancellationToken).ConfigureAwait(false);
    }
}
