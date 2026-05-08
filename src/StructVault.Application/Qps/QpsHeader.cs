namespace StructVault.Application.Qps;

public sealed class QpsHeader
{
    public QpsHeader(
        byte version,
        int saltLength,
        int initializationVectorLength,
        int authenticationTagLength,
        long ciphertextLength)
    {
        Validate(version, saltLength, initializationVectorLength, authenticationTagLength, ciphertextLength);

        Version = version;
        SaltLength = saltLength;
        InitializationVectorLength = initializationVectorLength;
        AuthenticationTagLength = authenticationTagLength;
        CiphertextLength = ciphertextLength;
    }

    public byte Version { get; }

    public int SaltLength { get; }

    public int InitializationVectorLength { get; }

    public int AuthenticationTagLength { get; }

    public long CiphertextLength { get; }

    public long RequiredFileLength => checked(
        QpsFileFormat.HeaderSizeInBytes +
        SaltLength +
        InitializationVectorLength +
        CiphertextLength +
        AuthenticationTagLength);

    private static void Validate(
        byte version,
        int saltLength,
        int initializationVectorLength,
        int authenticationTagLength,
        long ciphertextLength)
    {
        QpsFileFormat.EnsureSupportedVersion(version);

        if (saltLength < QpsFileFormat.MinimumSaltSizeInBytes)
        {
            throw new ArgumentException(
                $"QPS header salt length must be at least {QpsFileFormat.MinimumSaltSizeInBytes} bytes.",
                nameof(saltLength));
        }

        if (saltLength > ushort.MaxValue)
        {
            throw new ArgumentException("QPS header salt length exceeds the file format limit.", nameof(saltLength));
        }

        if (initializationVectorLength != QpsFileFormat.InitializationVectorSizeInBytes)
        {
            throw new ArgumentException(
                $"QPS header initialization vector length must be {QpsFileFormat.InitializationVectorSizeInBytes} bytes.",
                nameof(initializationVectorLength));
        }

        if (authenticationTagLength != QpsFileFormat.AuthenticationTagSizeInBytes)
        {
            throw new ArgumentException(
                $"QPS header authentication tag length must be {QpsFileFormat.AuthenticationTagSizeInBytes} bytes.",
                nameof(authenticationTagLength));
        }

        if (ciphertextLength <= 0)
        {
            throw new ArgumentException("QPS header must reference encrypted vault data.", nameof(ciphertextLength));
        }

        if (ciphertextLength > int.MaxValue)
        {
            throw new ArgumentException("QPS encrypted data length exceeds the supported in-memory limit.", nameof(ciphertextLength));
        }
    }
}
