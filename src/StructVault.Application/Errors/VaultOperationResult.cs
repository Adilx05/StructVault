namespace StructVault.Application.Errors;

public sealed class VaultOperationResult
{
    private VaultOperationResult(bool isSuccess, VaultOperationError? error)
    {
        if (isSuccess == (error is not null))
        {
            throw new ArgumentException("Successful results cannot contain errors and failed results must contain an error.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public VaultOperationError? Error { get; }

    public static VaultOperationResult Success()
    {
        return new VaultOperationResult(true, null);
    }

    public static VaultOperationResult Failure(VaultOperationError error)
    {
        return new VaultOperationResult(false, error ?? throw new ArgumentNullException(nameof(error)));
    }
}
