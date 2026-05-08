using System.Buffers.Binary;

namespace StructVault.Application.Qps;

public static class QpsFileFormat
{
    public const byte CurrentVersion = 1;
    public const int HeaderSizeInBytes = 13;
    public const int MinimumSaltSizeInBytes = 16;
    public const int InitializationVectorSizeInBytes = 12;
    public const int AuthenticationTagSizeInBytes = 16;

    public static ReadOnlySpan<byte> Magic => "QPSV"u8;

    public static bool HasSupportedMagic(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= Magic.Length && bytes[..Magic.Length].SequenceEqual(Magic);
    }

    internal static QpsHeader ReadHeader(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderSizeInBytes)
        {
            throw new ArgumentException($"QPS data must contain a complete {HeaderSizeInBytes}-byte header.", nameof(bytes));
        }

        if (!HasSupportedMagic(bytes))
        {
            throw new ArgumentException("QPS data does not contain a supported file signature.", nameof(bytes));
        }

        byte version = bytes[4];
        if (version != CurrentVersion)
        {
            throw new NotSupportedException($"QPS file version {version} is not supported.");
        }

        int saltLength = BinaryPrimitives.ReadUInt16BigEndian(bytes[5..7]);
        int initializationVectorLength = bytes[7];
        int authenticationTagLength = bytes[8];
        long ciphertextLength = BinaryPrimitives.ReadUInt32BigEndian(bytes[9..13]);

        return new QpsHeader(
            version,
            saltLength,
            initializationVectorLength,
            authenticationTagLength,
            ciphertextLength);
    }

    internal static void ValidateFileLength(ReadOnlySpan<byte> bytes, QpsHeader header, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(header);

        if (bytes.Length != header.RequiredFileLength)
        {
            throw new ArgumentException("QPS data length does not match the header metadata.", parameterName);
        }
    }

    internal static void WriteHeader(Span<byte> destination, QpsHeader header)
    {
        if (destination.Length < HeaderSizeInBytes)
        {
            throw new ArgumentException($"Destination must be at least {HeaderSizeInBytes} bytes.", nameof(destination));
        }

        Magic.CopyTo(destination);
        destination[4] = header.Version;
        BinaryPrimitives.WriteUInt16BigEndian(destination[5..7], checked((ushort)header.SaltLength));
        destination[7] = checked((byte)header.InitializationVectorLength);
        destination[8] = checked((byte)header.AuthenticationTagLength);
        BinaryPrimitives.WriteUInt32BigEndian(destination[9..13], checked((uint)header.CiphertextLength));
    }
}
