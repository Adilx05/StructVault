using MediatR;
using StructVault.Application.Clipboard;
using StructVault.Application.IdleLock;
using StructVault.Application.Persistence;
using StructVault.Application.Qps;
using StructVault.Infrastructure.Security;
using StructVault.Infrastructure.Storage;
using StructVault.Desktop.Services;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;

namespace StructVault.Desktop.Composition;

internal sealed class DesktopVaultSender : ISender
{
    private readonly SqliteVaultNodeWriter nodeStore = new();
    private readonly SqliteVaultFieldWriter fieldStore = new();
    private readonly SqliteVaultDatabaseSerializer databaseSerializer;
    private readonly Argon2idKeyDerivationService keyDerivationService = new();
    private readonly Aes256GcmEncryptionService encryptionService = new();
    private readonly FileSystemQpsFileReader fileReader = new();
    private readonly FileSystemQpsFileBackupService backupService = new();
    private readonly FileSystemQpsFileWriter fileWriter = new();
    private readonly WpfClipboardService clipboardService = new();
    private readonly ClipboardAutoClearService clipboardAutoClearService;
    private readonly IdleActivityTracker idleActivityTracker = new();

    public DesktopVaultSender()
    {
        databaseSerializer = new SqliteVaultDatabaseSerializer(new SqliteVaultSchemaProvider());
        clipboardAutoClearService = new ClipboardAutoClearService(clipboardService, new SystemClipboardClearDelay());
    }

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        object? response = request switch
        {
            CreateInMemoryVaultDatabaseCommand command => await new CreateInMemoryVaultDatabaseCommandHandler(
                    new SqliteInMemoryVaultDatabaseConnectionFactory(new SqliteVaultSchemaProvider()))
                .Handle(command, cancellationToken)
                .ConfigureAwait(false),
            DeserializeVaultDatabaseCommand command => await new DeserializeVaultDatabaseCommandHandler(databaseSerializer)
                .Handle(command, cancellationToken)
                .ConfigureAwait(false),
            SerializeVaultDatabaseQuery query => await new SerializeVaultDatabaseQueryHandler(databaseSerializer)
                .Handle(query, cancellationToken)
                .ConfigureAwait(false),
            ListVaultNodeHierarchyQuery query => await new ListVaultNodeHierarchyQueryHandler(nodeStore)
                .Handle(query, cancellationToken)
                .ConfigureAwait(false),
            ListVaultNodesQuery query => await new ListVaultNodesQueryHandler(nodeStore)
                .Handle(query, cancellationToken)
                .ConfigureAwait(false),
            SearchVaultQuery query => await new SearchVaultQueryHandler(nodeStore, fieldStore)
                .Handle(query, cancellationToken)
                .ConfigureAwait(false),
            GetVaultNodeByIdQuery query => await new GetVaultNodeByIdQueryHandler(nodeStore)
                .Handle(query, cancellationToken)
                .ConfigureAwait(false),
            ListVaultFieldsByNodeIdQuery query => await new ListVaultFieldsByNodeIdQueryHandler(fieldStore)
                .Handle(query, cancellationToken)
                .ConfigureAwait(false),
            GetVaultFieldByIdQuery query => await new GetVaultFieldByIdQueryHandler(fieldStore)
                .Handle(query, cancellationToken)
                .ConfigureAwait(false),
            GetVaultSchemaQuery query => await new GetVaultSchemaQueryHandler(new SqliteVaultSchemaProvider())
                .Handle(query, cancellationToken)
                .ConfigureAwait(false),
            UpdateVaultNodeCommand command => await new UpdateVaultNodeCommandHandler(nodeStore)
                .Handle(command, cancellationToken)
                .ConfigureAwait(false),
            UpdateVaultFieldCommand command => await new UpdateVaultFieldCommandHandler(fieldStore)
                .Handle(command, cancellationToken)
                .ConfigureAwait(false),
            ReorderVaultFieldCommand command => await new ReorderVaultFieldCommandHandler(fieldStore)
                .Handle(command, cancellationToken)
                .ConfigureAwait(false),
            CreateQpsVaultFileCommand command => await new CreateQpsVaultFileCommandHandler()
                .Handle(command, cancellationToken)
                .ConfigureAwait(false),
            OpenQpsVaultFileQuery query => await new OpenQpsVaultFileQueryHandler(fileReader, keyDerivationService, encryptionService)
                .Handle(query, cancellationToken)
                .ConfigureAwait(false),
            ParseQpsVaultFileQuery query => await new ParseQpsVaultFileQueryHandler()
                .Handle(query, cancellationToken)
                .ConfigureAwait(false),
            ParseQpsVaultFileHeaderQuery query => await new ParseQpsVaultFileHeaderQueryHandler()
                .Handle(query, cancellationToken)
                .ConfigureAwait(false),
            GetQpsFileVersionSupportQuery query => await new GetQpsFileVersionSupportQueryHandler()
                .Handle(query, cancellationToken)
                .ConfigureAwait(false),
            ReadQpsVaultFileQuery query => await new ReadQpsVaultFileQueryHandler(fileReader)
                .Handle(query, cancellationToken)
                .ConfigureAwait(false),
            RecordUserActivityCommand command => await new RecordUserActivityCommandHandler(idleActivityTracker)
                .Handle(command, cancellationToken)
                .ConfigureAwait(false),
            GetIdleActivityStateQuery query => await new GetIdleActivityStateQueryHandler(idleActivityTracker)
                .Handle(query, cancellationToken)
                .ConfigureAwait(false),
            LockVaultAfterIdleTimeoutCommand command => await new LockVaultAfterIdleTimeoutCommandHandler(idleActivityTracker)
                .Handle(command, cancellationToken)
                .ConfigureAwait(false),
            _ => throw CreateUnsupportedRequestException(request)
        };

        return (TResponse)response!;
    }

    public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        ArgumentNullException.ThrowIfNull(request);

        switch (request)
        {
            case CreateVaultNodeCommand command:
                await new CreateVaultNodeCommandHandler(nodeStore).Handle(command, cancellationToken).ConfigureAwait(false);
                break;
            case DeleteVaultNodeCommand command:
                await new DeleteVaultNodeCommandHandler(nodeStore).Handle(command, cancellationToken).ConfigureAwait(false);
                break;
            case CreateVaultFieldCommand command:
                await new CreateVaultFieldCommandHandler(fieldStore).Handle(command, cancellationToken).ConfigureAwait(false);
                break;
            case DeleteVaultFieldCommand command:
                await new DeleteVaultFieldCommandHandler(fieldStore).Handle(command, cancellationToken).ConfigureAwait(false);
                break;
            case CopyVaultFieldValueToClipboardCommand command:
                await new CopyVaultFieldValueToClipboardCommandHandler(fieldStore, clipboardService, clipboardAutoClearService)
                    .Handle(command, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case SaveQpsVaultFileCommand command:
                await new SaveQpsVaultFileCommandHandler(databaseSerializer, keyDerivationService, encryptionService, backupService, fileWriter)
                    .Handle(command, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case CreateQpsVaultFileBackupCommand command:
                await new CreateQpsVaultFileBackupCommandHandler(backupService).Handle(command, cancellationToken).ConfigureAwait(false);
                break;
            case RestoreQpsVaultFileBackupCommand command:
                await new RestoreQpsVaultFileBackupCommandHandler(backupService).Handle(command, cancellationToken).ConfigureAwait(false);
                break;
            case WriteQpsVaultFileCommand command:
                await new WriteQpsVaultFileCommandHandler(fileWriter).Handle(command, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw CreateUnsupportedRequestException(request);
        }
    }

    public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        switch (request)
        {
            case CreateInMemoryVaultDatabaseCommand command:
                return await Send(command, cancellationToken).ConfigureAwait(false);
            case DeserializeVaultDatabaseCommand command:
                return await Send(command, cancellationToken).ConfigureAwait(false);
            case SerializeVaultDatabaseQuery query:
                return await Send(query, cancellationToken).ConfigureAwait(false);
            case ListVaultNodeHierarchyQuery query:
                return await Send(query, cancellationToken).ConfigureAwait(false);
            case ListVaultNodesQuery query:
                return await Send(query, cancellationToken).ConfigureAwait(false);
            case SearchVaultQuery query:
                return await Send(query, cancellationToken).ConfigureAwait(false);
            case GetVaultNodeByIdQuery query:
                return await Send(query, cancellationToken).ConfigureAwait(false);
            case ListVaultFieldsByNodeIdQuery query:
                return await Send(query, cancellationToken).ConfigureAwait(false);
            case GetVaultFieldByIdQuery query:
                return await Send(query, cancellationToken).ConfigureAwait(false);
            case GetVaultSchemaQuery query:
                return await Send(query, cancellationToken).ConfigureAwait(false);
            case UpdateVaultNodeCommand command:
                return await Send(command, cancellationToken).ConfigureAwait(false);
            case UpdateVaultFieldCommand command:
                return await Send(command, cancellationToken).ConfigureAwait(false);
            case ReorderVaultFieldCommand command:
                return await Send(command, cancellationToken).ConfigureAwait(false);
            case CreateQpsVaultFileCommand command:
                return await Send(command, cancellationToken).ConfigureAwait(false);
            case OpenQpsVaultFileQuery query:
                return await Send(query, cancellationToken).ConfigureAwait(false);
            case ParseQpsVaultFileQuery query:
                return await Send(query, cancellationToken).ConfigureAwait(false);
            case ParseQpsVaultFileHeaderQuery query:
                return await Send(query, cancellationToken).ConfigureAwait(false);
            case GetQpsFileVersionSupportQuery query:
                return await Send(query, cancellationToken).ConfigureAwait(false);
            case ReadQpsVaultFileQuery query:
                return await Send(query, cancellationToken).ConfigureAwait(false);
            case RecordUserActivityCommand command:
                return await Send(command, cancellationToken).ConfigureAwait(false);
            case GetIdleActivityStateQuery query:
                return await Send(query, cancellationToken).ConfigureAwait(false);
            case LockVaultAfterIdleTimeoutCommand command:
                return await Send(command, cancellationToken).ConfigureAwait(false);
            case CreateQpsVaultFileBackupCommand command:
                await Send(command, cancellationToken).ConfigureAwait(false);
                return null;
            case RestoreQpsVaultFileBackupCommand command:
                await Send(command, cancellationToken).ConfigureAwait(false);
                return null;
            case IRequest command:
                await Send(command, cancellationToken).ConfigureAwait(false);
                return null;
            default:
                throw CreateUnsupportedRequestException(request);
        }
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("StructVault desktop does not use streaming mediator requests.");
    }

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("StructVault desktop does not use streaming mediator requests.");
    }

    private static NotSupportedException CreateUnsupportedRequestException(object request)
    {
        return new NotSupportedException($"Unsupported desktop mediator request type '{request.GetType().Name}'.");
    }
}
