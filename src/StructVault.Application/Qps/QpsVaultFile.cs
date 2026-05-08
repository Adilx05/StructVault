namespace StructVault.Application.Qps;

public sealed class QpsVaultFile
{
    private readonly byte[] salt;
    private readonly byte[] initializationVector;
    private readonly byte[] ciphertext;
    private readonly byte[] authenticationTag;

    public QpsVaultFile(
        byte version,
        byte[] salt,
        byte[] initializationVector,
        byte[] ciphertext,
        byte[] authenticationTag)
    {
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(initializationVector);
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(authenticationTag);

        Validate(version, salt, initializationVector, ciphertext, authenticationTag);

        Version = version;
        this.salt = salt.ToArray();
        this.initializationVector = initializationVector.ToArray();
        this.ciphertext = ciphertext.ToArray();
        this.authenticationTag = authenticationTag.ToArray();
    }

    public byte Version { get; }

    public ReadOnlyMemory<byte> Salt => salt;

    public ReadOnlyMemory<byte> InitializationVector => initializationVector;

    public ReadOnlyMemory<byte> Ciphertext => ciphertext;

    public ReadOnlyMemory<byte> AuthenticationTag => authenticationTag;

    private static void Validate(
        byte version,
        byte[] salt,
        byte[] initializationVector,
        byte[] ciphertext,
        byte[] authenticationTag)
    {
        QpsFileFormat.EnsureSupportedVersion(version);

        if (salt.Length < QpsFileFormat.MinimumSaltSizeInBytes)
        {
            throw new ArgumentException(
                $"QPS files require an Argon2 salt of at least {QpsFileFormat.MinimumSaltSizeInBytes} bytes.",
                nameof(salt));
        }

        if (initializationVector.Length != QpsFileFormat.InitializationVectorSizeInBytes)
        {
            throw new ArgumentException(
                $"QPS files require a {QpsFileFormat.InitializationVectorSizeInBytes}-byte AES-GCM initialization vector.",
                nameof(initializationVector));
        }

        if (ciphertext.Length == 0)
        {
            throw new ArgumentException("QPS files require encrypted vault data.", nameof(ciphertext));
        }

        if (authenticationTag.Length != QpsFileFormat.AuthenticationTagSizeInBytes)
        {
            throw new ArgumentException(
                $"QPS files require a {QpsFileFormat.AuthenticationTagSizeInBytes}-byte AES-GCM authentication tag.",
                nameof(authenticationTag));
        }
    }
}
