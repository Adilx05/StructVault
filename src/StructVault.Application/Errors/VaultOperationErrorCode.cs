namespace StructVault.Application.Errors;

public enum VaultOperationErrorCode
{
    ValidationFailed,
    AuthenticationFailed,
    FileAccessFailed,
    UnsupportedFormat,
    CorruptedVault,
    PersistenceFailed,
    UnexpectedFailure
}
