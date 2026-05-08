using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class CreateVaultNodeCommandHandler : IRequestHandler<CreateVaultNodeCommand>
{
    private readonly IVaultNodeWriter nodeWriter;

    public CreateVaultNodeCommandHandler(IVaultNodeWriter nodeWriter)
    {
        this.nodeWriter = nodeWriter ?? throw new ArgumentNullException(nameof(nodeWriter));
    }

    public async Task Handle(CreateVaultNodeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await nodeWriter.CreateAsync(request.Connection, request, cancellationToken).ConfigureAwait(false);
    }
}
