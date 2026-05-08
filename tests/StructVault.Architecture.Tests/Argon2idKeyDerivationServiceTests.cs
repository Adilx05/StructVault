using StructVault.Application.Abstractions.Security;
using StructVault.Application.Security;
using StructVault.Infrastructure.Security;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class Argon2idKeyDerivationServiceTests
{
    private static readonly byte[] ValidSalt =
    [
        0x10, 0x21, 0x32, 0x43,
        0x54, 0x65, 0x76, 0x87,
        0x98, 0xA9, 0xBA, 0xCB,
        0xDC, 0xED, 0xFE, 0x0F,
    ];

    [Fact]
    public void DeriveKeyReturnsAes256SizedKey()
    {
        Argon2idKeyDerivationService service = new();

        byte[] key = service.DeriveKey("correct horse battery staple", ValidSalt);

        Assert.Equal(Argon2idKeyDerivationService.DerivedKeySizeInBytes, key.Length);
        Assert.Equal(32, service.KeySizeInBytes);
    }

    [Fact]
    public void DeriveKeyReturnsSameKeyForSamePasswordAndSalt()
    {
        Argon2idKeyDerivationService service = new();

        byte[] firstKey = service.DeriveKey("correct horse battery staple", ValidSalt);
        byte[] secondKey = service.DeriveKey("correct horse battery staple", ValidSalt);

        Assert.Equal(firstKey, secondKey);
    }

    [Fact]
    public void DeriveKeyReturnsDifferentKeyForDifferentPassword()
    {
        Argon2idKeyDerivationService service = new();

        byte[] firstKey = service.DeriveKey("correct horse battery staple", ValidSalt);
        byte[] secondKey = service.DeriveKey("different horse battery staple", ValidSalt);

        Assert.NotEqual(firstKey, secondKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DeriveKeyRequiresNonEmptyPassword(string password)
    {
        Argon2idKeyDerivationService service = new();

        Assert.Throws<ArgumentException>(() => service.DeriveKey(password, ValidSalt));
    }

    [Fact]
    public void DeriveKeyRequiresMinimumSaltLength()
    {
        Argon2idKeyDerivationService service = new();
        byte[] shortSalt = new byte[Argon2idKeyDerivationService.MinimumSaltSizeInBytes - 1];

        Assert.Throws<ArgumentException>(() => service.DeriveKey("correct horse battery staple", shortSalt));
    }

    [Fact]
    public async Task HandlerDerivesKeyThroughApplicationContract()
    {
        byte[] expectedKey = [1, 2, 3, 4];
        RecordingKeyDerivationService service = new(expectedKey);
        DeriveVaultKeyCommandHandler handler = new(service);
        DeriveVaultKeyCommand command = new("correct horse battery staple", ValidSalt);

        byte[] actualKey = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(expectedKey, actualKey);
        Assert.Equal(command.Password, service.Password);
        Assert.Equal(ValidSalt, service.Salt);
    }

    [Fact]
    public async Task HandlerRejectsEmptyPasswordBeforeDerivingKey()
    {
        RecordingKeyDerivationService service = new([1, 2, 3, 4]);
        DeriveVaultKeyCommandHandler handler = new(service);
        DeriveVaultKeyCommand command = new(string.Empty, ValidSalt);

        await Assert.ThrowsAsync<ArgumentException>(async () => await handler.Handle(command, CancellationToken.None));

        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task HandlerRejectsShortSaltBeforeDerivingKey()
    {
        RecordingKeyDerivationService service = new([1, 2, 3, 4]);
        DeriveVaultKeyCommandHandler handler = new(service);
        DeriveVaultKeyCommand command = new("correct horse battery staple", new byte[15]);

        await Assert.ThrowsAsync<ArgumentException>(async () => await handler.Handle(command, CancellationToken.None));

        Assert.False(service.WasCalled);
    }

    [Fact]
    public void CommandCopiesSaltDefensively()
    {
        byte[] salt = ValidSalt.ToArray();
        DeriveVaultKeyCommand command = new("correct horse battery staple", salt);

        salt[0] = 0xFF;

        Assert.Equal(ValidSalt, command.Salt.ToArray());
    }

    private sealed class RecordingKeyDerivationService : IKeyDerivationService
    {
        private readonly byte[] key;

        public RecordingKeyDerivationService(byte[] key)
        {
            this.key = key;
        }

        public int KeySizeInBytes => key.Length;

        public string? Password { get; private set; }

        public byte[]? Salt { get; private set; }

        public bool WasCalled { get; private set; }

        public byte[] DeriveKey(string password, ReadOnlyMemory<byte> salt)
        {
            WasCalled = true;
            Password = password;
            Salt = salt.ToArray();
            return key.ToArray();
        }
    }
}
