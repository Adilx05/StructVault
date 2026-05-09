using MediatR;
using StructVault.Application.Abstractions.Logging;

namespace StructVault.Application.Logging;

public sealed class WriteApplicationLogCommandHandler : IRequestHandler<WriteApplicationLogCommand>
{
    private readonly IApplicationLogWriter logWriter;

    public WriteApplicationLogCommandHandler(IApplicationLogWriter logWriter)
    {
        this.logWriter = logWriter ?? throw new ArgumentNullException(nameof(logWriter));
    }

    public async Task Handle(WriteApplicationLogCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await logWriter.WriteAsync(request.Entry, cancellationToken).ConfigureAwait(false);
    }
}
