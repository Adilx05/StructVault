using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class GetVaultSchemaQueryHandler : IRequestHandler<GetVaultSchemaQuery, string>
{
    private readonly IVaultSchemaProvider schemaProvider;

    public GetVaultSchemaQueryHandler(IVaultSchemaProvider schemaProvider)
    {
        this.schemaProvider = schemaProvider ?? throw new ArgumentNullException(nameof(schemaProvider));
    }

    public Task<string> Handle(GetVaultSchemaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string schemaScript = schemaProvider.GetCreateSchemaScript();
        if (string.IsNullOrWhiteSpace(schemaScript))
        {
            throw new InvalidOperationException("Vault schema provider returned an empty SQLite schema script.");
        }

        return Task.FromResult(schemaScript);
    }
}
