using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class UpdateVaultNodeCommandHandler : IRequestHandler<UpdateVaultNodeCommand, bool>
{
    private readonly IVaultNodeWriter nodeWriter;

    public UpdateVaultNodeCommandHandler(IVaultNodeWriter nodeWriter)
    {
        this.nodeWriter = nodeWriter ?? throw new ArgumentNullException(nameof(nodeWriter));
    }

    public async Task<bool> Handle(UpdateVaultNodeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return await nodeWriter.UpdateAsync(request.Connection, request, cancellationToken).ConfigureAwait(false);
    }
}
