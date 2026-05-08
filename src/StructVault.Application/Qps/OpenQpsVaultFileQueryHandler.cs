using System.Security.Cryptography;
using MediatR;
using StructVault.Application.Abstractions.Security;
using StructVault.Application.Abstractions.Storage;

namespace StructVault.Application.Qps;

public sealed class OpenQpsVaultFileQueryHandler : IRequestHandler<OpenQpsVaultFileQuery, byte[]>
{
    private readonly IQpsFileReader fileReader;
    private readonly IKeyDerivationService keyDerivationService;
    private readonly IAuthenticatedEncryptionService encryptionService;

    public OpenQpsVaultFileQueryHandler(
        IQpsFileReader fileReader,
        IKeyDerivationService keyDerivationService,
        IAuthenticatedEncryptionService encryptionService)
    {
        this.fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));
        this.keyDerivationService = keyDerivationService ?? throw new ArgumentNullException(nameof(keyDerivationService));
        this.encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
    }

    public async Task<byte[]> Handle(OpenQpsVaultFileQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);

        byte[] fileBytes = await fileReader.ReadAsync(request.FilePath, cancellationToken).ConfigureAwait(false);
        if (fileBytes.Length == 0)
        {
            throw new ArgumentException("QPS vault file is empty.", nameof(request));
        }

        QpsVaultFile vaultFile = ParseQpsVaultFileQueryHandler.Parse(fileBytes);
        byte[] key = keyDerivationService.DeriveKey(request.Password, vaultFile.Salt);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return encryptionService.Decrypt(
                vaultFile.Ciphertext,
                key,
                vaultFile.InitializationVector,
                vaultFile.AuthenticationTag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static void Validate(OpenQpsVaultFileQuery request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            throw new ArgumentException("A QPS vault file path is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("A non-empty password is required.", nameof(request));
        }
    }
}
