using System.Security.Cryptography;
using MediatR;
using StructVault.Application.Abstractions.Security;
using StructVault.Application.Abstractions.Storage;
using StructVault.Application.Qps;

namespace StructVault.Application.IdleLock;

public sealed class UnlockVaultCommandHandler : IRequestHandler<UnlockVaultCommand, bool>
{
    private readonly IQpsFileReader fileReader;
    private readonly IKeyDerivationService keyDerivationService;
    private readonly IAuthenticatedEncryptionService encryptionService;

    public UnlockVaultCommandHandler(
        IQpsFileReader fileReader,
        IKeyDerivationService keyDerivationService,
        IAuthenticatedEncryptionService encryptionService)
    {
        this.fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));
        this.keyDerivationService = keyDerivationService ?? throw new ArgumentNullException(nameof(keyDerivationService));
        this.encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
    }

    public async Task<bool> Handle(UnlockVaultCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);

        byte[]? key = null;
        byte[]? plaintext = null;

        try
        {
            byte[] fileBytes = await fileReader.ReadAsync(request.FilePath, cancellationToken).ConfigureAwait(false);
            if (fileBytes.Length == 0)
            {
                return false;
            }

            QpsVaultFile vaultFile = ParseQpsVaultFileQueryHandler.Parse(fileBytes);
            key = keyDerivationService.DeriveKey(request.Password, vaultFile.Salt);
            cancellationToken.ThrowIfCancellationRequested();
            plaintext = encryptionService.Decrypt(
                vaultFile.Ciphertext,
                key,
                vaultFile.InitializationVector,
                vaultFile.AuthenticationTag);

            return plaintext.Length > 0;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            ZeroIfPresent(key);
            ZeroIfPresent(plaintext);
        }
    }

    private static void Validate(UnlockVaultCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            throw new ArgumentException("A QPS vault file path is required to unlock the vault.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("A non-empty password is required to unlock the vault.", nameof(request));
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
