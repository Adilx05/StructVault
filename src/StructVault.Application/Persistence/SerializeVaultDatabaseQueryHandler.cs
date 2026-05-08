using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class SerializeVaultDatabaseQueryHandler : IRequestHandler<SerializeVaultDatabaseQuery, byte[]>
{
    private readonly IVaultDatabaseSerializer databaseSerializer;

    public SerializeVaultDatabaseQueryHandler(IVaultDatabaseSerializer databaseSerializer)
    {
        this.databaseSerializer = databaseSerializer ?? throw new ArgumentNullException(nameof(databaseSerializer));
    }

    public async Task<byte[]> Handle(SerializeVaultDatabaseQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        byte[] databaseImage = await databaseSerializer.SerializeAsync(request.Connection, cancellationToken).ConfigureAwait(false);
        if (databaseImage.Length == 0)
        {
            throw new InvalidOperationException("Vault database serializer returned an empty SQLite database image.");
        }

        return databaseImage;
    }
}
