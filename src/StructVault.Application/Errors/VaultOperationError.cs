namespace StructVault.Application.Errors;

public sealed record VaultOperationError
{
    public VaultOperationError(VaultOperationErrorCode code, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("An operation error message is required.", nameof(message));
        }

        Code = code;
        Message = message;
    }

    public VaultOperationErrorCode Code { get; }

    public string Message { get; }
}
