using MediatR;

namespace StructVault.Application.Qps;

public sealed class GetQpsFileVersionSupportQueryHandler : IRequestHandler<GetQpsFileVersionSupportQuery, QpsFileVersionSupport>
{
    public Task<QpsFileVersionSupport> Handle(GetQpsFileVersionSupportQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(QpsFileFormat.VersionSupport);
    }
}
