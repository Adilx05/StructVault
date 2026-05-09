using System.Security.Cryptography;

namespace StructVault.Application.Errors;

public static class VaultOperationErrorMapper
{
    public static VaultOperationError FromException(Exception exception, string actionDescription)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (string.IsNullOrWhiteSpace(actionDescription))
        {
            throw new ArgumentException("An action description is required.", nameof(actionDescription));
        }

        return exception switch
        {
            OperationCanceledException => throw exception,
            CryptographicException => new VaultOperationError(
                VaultOperationErrorCode.AuthenticationFailed,
                $"The vault could not be {actionDescription}. Check the password and verify the vault file has not been tampered with."),
            UnauthorizedAccessException => new VaultOperationError(
                VaultOperationErrorCode.FileAccessFailed,
                $"The vault could not be {actionDescription} because the file cannot be accessed."),
            IOException => new VaultOperationError(
                VaultOperationErrorCode.FileAccessFailed,
                $"The vault could not be {actionDescription} because the vault file could not be read or written."),
            NotSupportedException => new VaultOperationError(
                VaultOperationErrorCode.UnsupportedFormat,
                $"The vault could not be {actionDescription} because the vault file format is not supported."),
            ArgumentException => new VaultOperationError(
                VaultOperationErrorCode.ValidationFailed,
                $"The vault could not be {actionDescription} because the request is invalid."),
            InvalidOperationException => new VaultOperationError(
                VaultOperationErrorCode.PersistenceFailed,
                $"The vault could not be {actionDescription} because the vault data operation failed."),
            _ => new VaultOperationError(
                VaultOperationErrorCode.UnexpectedFailure,
                $"The vault could not be {actionDescription} because an unexpected error occurred.")
        };
    }
}
