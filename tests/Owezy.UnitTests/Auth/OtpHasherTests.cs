using Owezy.Application.Auth;
using Xunit;

namespace Owezy.UnitTests.Auth;

public class OtpHasherTests
{
    [Fact]
    public void HashOtp_ValidCode_ProducesSha256HexHash()
    {
        var hasher = new Sha256OtpHasher();
        var code = "004217";

        var hash = hasher.HashOtp(code);

        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length); // 256 bits = 32 bytes = 64 hex chars
    }

    [Fact]
    public void VerifyHash_MatchingCode_ReturnsTrue()
    {
        var hasher = new Sha256OtpHasher();
        var code = "123456";
        var hash = hasher.HashOtp(code);

        var isValid = hasher.VerifyHash("123456", hash);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyHash_NonMatchingCode_ReturnsFalse()
    {
        var hasher = new Sha256OtpHasher();
        var hash = hasher.HashOtp("123456");

        var isValid = hasher.VerifyHash("654321", hash);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HashOtp_EmptyCode_ThrowsArgumentException(string? emptyCode)
    {
        var hasher = new Sha256OtpHasher();
        Assert.Throws<ArgumentException>(() => hasher.HashOtp(emptyCode!));
    }
}
