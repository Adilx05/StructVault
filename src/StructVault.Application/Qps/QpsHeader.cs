namespace StructVault.Application.Qps;

internal readonly record struct QpsHeader(
    byte Version,
    int SaltLength,
    int InitializationVectorLength,
    int AuthenticationTagLength,
    long CiphertextLength);
