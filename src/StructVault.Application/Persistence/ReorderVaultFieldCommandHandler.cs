using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class ReorderVaultFieldCommandHandler : IRequestHandler<ReorderVaultFieldCommand, bool>
{
    private readonly IVaultFieldWriter fieldWriter;

    public ReorderVaultFieldCommandHandler(IVaultFieldWriter fieldWriter)
    {
        this.fieldWriter = fieldWriter ?? throw new ArgumentNullException(nameof(fieldWriter));
    }

    public async Task<bool> Handle(ReorderVaultFieldCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return await fieldWriter.ReorderAsync(request.Connection, request, cancellationToken).ConfigureAwait(false);
    }
}
