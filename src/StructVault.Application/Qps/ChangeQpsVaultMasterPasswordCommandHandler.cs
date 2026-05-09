using System.Security.Cryptography;
using MediatR;
using StructVault.Application.Abstractions.Security;
using StructVault.Application.Abstractions.Storage;

namespace StructVault.Application.Qps;

public sealed class ChangeQpsVaultMasterPasswordCommandHandler : IRequestHandler<ChangeQpsVaultMasterPasswordCommand>
{
    private const int SaltSizeInBytes = 16;

    private readonly IQpsFileReader fileReader;
    private readonly IKeyDerivationService keyDerivationService;
    private readonly IAuthenticatedEncryptionService encryptionService;
    private readonly IQpsFileBackupService backupService;
    private readonly IQpsFileWriter fileWriter;

    public ChangeQpsVaultMasterPasswordCommandHandler(
        IQpsFileReader fileReader,
        IKeyDerivationService keyDerivationService,
        IAuthenticatedEncryptionService encryptionService,
        IQpsFileBackupService backupService,
        IQpsFileWriter fileWriter)
    {
        this.fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));
        this.keyDerivationService = keyDerivationService ?? throw new ArgumentNullException(nameof(keyDerivationService));
        this.encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        this.backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        this.fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    public async Task Handle(ChangeQpsVaultMasterPasswordCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);

        byte[]? fileBytes = null;
        byte[]? plaintextVaultData = null;
        byte[]? currentKey = null;
        byte[]? newSalt = null;
        byte[]? newKey = null;
        byte[]? newQpsFileBytes = null;

        try
        {
            fileBytes = await fileReader.ReadAsync(request.FilePath, cancellationToken).ConfigureAwait(false);
            if (fileBytes.Length == 0)
            {
                throw new ArgumentException("QPS vault file is empty.", nameof(request));
            }

            QpsVaultFile vaultFile = ParseQpsVaultFileQueryHandler.Parse(fileBytes);
            currentKey = keyDerivationService.DeriveKey(request.CurrentPassword, vaultFile.Salt);

            cancellationToken.ThrowIfCancellationRequested();
            plaintextVaultData = encryptionService.Decrypt(
                vaultFile.Ciphertext,
                currentKey,
                vaultFile.InitializationVector,
                vaultFile.AuthenticationTag);

            if (plaintextVaultData.Length == 0)
            {
                throw new InvalidOperationException("Vault decryption returned an empty SQLite database image.");
            }

            newSalt = RandomNumberGenerator.GetBytes(SaltSizeInBytes);
            newKey = keyDerivationService.DeriveKey(request.NewPassword, newSalt);

            cancellationToken.ThrowIfCancellationRequested();
            AesGcmEncryptionResult encryptionResult = encryptionService.Encrypt(plaintextVaultData, newKey);
            newQpsFileBytes = CreateQpsFileBytes(newSalt, encryptionResult);

            await backupService.BackupAsync(request.FilePath, cancellationToken).ConfigureAwait(false);
            await fileWriter.WriteAsync(request.FilePath, newQpsFileBytes, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ZeroIfPresent(fileBytes);
            ZeroIfPresent(plaintextVaultData);
            ZeroIfPresent(currentKey);
            ZeroIfPresent(newSalt);
            ZeroIfPresent(newKey);
            ZeroIfPresent(newQpsFileBytes);
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

    private static void Validate(ChangeQpsVaultMasterPasswordCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            throw new ArgumentException("A QPS vault file path is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            throw new ArgumentException("The current master password is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new ArgumentException("A non-empty new master password is required.", nameof(request));
        }

        if (request.CurrentPassword == request.NewPassword)
        {
            throw new ArgumentException("The new master password must be different from the current master password.", nameof(request));
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
