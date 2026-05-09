using System.Data;
using System.Security.Cryptography;
using MediatR;
using StructVault.Application.Abstractions.Persistence;
using StructVault.Application.Abstractions.Security;
using StructVault.Application.Abstractions.Storage;

namespace StructVault.Application.Qps;

public sealed class SaveQpsVaultFileCommandHandler : IRequestHandler<SaveQpsVaultFileCommand>
{
    private const int SaltSizeInBytes = 16;

    private readonly IVaultDatabaseSerializer databaseSerializer;
    private readonly IKeyDerivationService keyDerivationService;
    private readonly IAuthenticatedEncryptionService encryptionService;
    private readonly IQpsFileWriter fileWriter;

    public SaveQpsVaultFileCommandHandler(
        IVaultDatabaseSerializer databaseSerializer,
        IKeyDerivationService keyDerivationService,
        IAuthenticatedEncryptionService encryptionService,
        IQpsFileWriter fileWriter)
    {
        this.databaseSerializer = databaseSerializer ?? throw new ArgumentNullException(nameof(databaseSerializer));
        this.keyDerivationService = keyDerivationService ?? throw new ArgumentNullException(nameof(keyDerivationService));
        this.encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        this.fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    public async Task Handle(SaveQpsVaultFileCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);

        byte[]? databaseImage = null;
        byte[]? salt = null;
        byte[]? key = null;
        byte[]? qpsFileBytes = null;

        try
        {
            databaseImage = await databaseSerializer.SerializeAsync(request.Connection, cancellationToken).ConfigureAwait(false);
            if (databaseImage.Length == 0)
            {
                throw new InvalidOperationException("Vault database serializer returned an empty SQLite database image.");
            }

            salt = RandomNumberGenerator.GetBytes(SaltSizeInBytes);
            key = keyDerivationService.DeriveKey(request.Password, salt);
            AesGcmEncryptionResult encryptionResult = encryptionService.Encrypt(databaseImage, key);
            qpsFileBytes = CreateQpsFileBytes(salt, encryptionResult);

            await fileWriter.WriteAsync(request.FilePath, qpsFileBytes, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ZeroIfPresent(databaseImage);
            ZeroIfPresent(salt);
            ZeroIfPresent(key);
            ZeroIfPresent(qpsFileBytes);
        }
    }

    private static byte[] CreateQpsFileBytes(byte[] salt, AesGcmEncryptionResult encryptionResult)
    {
        QpsHeader header = new(
            QpsFileFormat.CurrentVersion,
            salt.Length,
            encryptionResult.Nonce.Length,
            encryptionResult.Tag.Length,
            encryptionResult.Ciphertext.Length);

        int fileLength = checked(
            QpsFileFormat.HeaderSizeInBytes +
            salt.Length +
            encryptionResult.Nonce.Length +
            encryptionResult.Ciphertext.Length +
            encryptionResult.Tag.Length);

        byte[] fileBytes = new byte[fileLength];
        Span<byte> destination = fileBytes;
        QpsFileFormat.WriteHeader(destination[..QpsFileFormat.HeaderSizeInBytes], header);

        int offset = QpsFileFormat.HeaderSizeInBytes;
        salt.AsSpan().CopyTo(destination[offset..]);
        offset += salt.Length;
        encryptionResult.Nonce.Span.CopyTo(destination[offset..]);
        offset += encryptionResult.Nonce.Length;
        encryptionResult.Ciphertext.Span.CopyTo(destination[offset..]);
        offset += encryptionResult.Ciphertext.Length;
        encryptionResult.Tag.Span.CopyTo(destination[offset..]);

        return fileBytes;
    }

    private static void Validate(SaveQpsVaultFileCommand request)
    {
        if (request.Connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Saving a QPS vault file requires an open vault database connection.");
        }

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            throw new ArgumentException("A QPS vault file path is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("A non-empty password is required to save a QPS vault file.", nameof(request));
        }
    }

    private static void ZeroIfPresent(byte[]? bytes)
    {
        if (bytes is { Length: > 0 })
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
