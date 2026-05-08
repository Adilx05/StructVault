using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class UpdateVaultFieldCommandHandler : IRequestHandler<UpdateVaultFieldCommand, bool>
{
    private readonly IVaultFieldWriter fieldWriter;

    public UpdateVaultFieldCommandHandler(IVaultFieldWriter fieldWriter)
    {
        this.fieldWriter = fieldWriter ?? throw new ArgumentNullException(nameof(fieldWriter));
    }

    public async Task<bool> Handle(UpdateVaultFieldCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return await fieldWriter.UpdateAsync(request.Connection, request, cancellationToken).ConfigureAwait(false);
    }
}
