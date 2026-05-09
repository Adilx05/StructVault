namespace StructVault.Application.Abstractions.Logging;

public interface IApplicationLogWriter
{
    Task WriteAsync(ApplicationLogEntry entry, CancellationToken cancellationToken);
}
