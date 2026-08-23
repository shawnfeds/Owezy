using Owezy.Application.Auth;
using Xunit;

namespace Owezy.UnitTests.Auth;

public class OtpHasherTests
{
    private const string DefaultTestSecret = "test-secret-key-1234567890123456";
    private const string AltTestSecret = "alternate-secret-key-9876543210987654";

    [Fact]
    public void Constructor_MissingOrEmptySecret_ThrowsInvalidOperationException()
    {
        var emptyOptions = new OtpHasherOptions { SecretKey = "" };
        Assert.Throws<InvalidOperationException>(() => new HmacSha256OtpHasher(emptyOptions));

        var whitespaceOptions = new OtpHasherOptions { SecretKey = "   " };
        Assert.Throws<InvalidOperationException>(() => new HmacSha256OtpHasher(whitespaceOptions));

        Assert.Throws<InvalidOperationException>(() => new HmacSha256OtpHasher(""));
        Assert.Throws<InvalidOperationException>(() => new HmacSha256OtpHasher("   "));
    }

    [Fact]
    public void HashOtp_ValidCode_ProducesHexHmacSha256Hash()
    {
        var hasher = new HmacSha256OtpHasher(DefaultTestSecret);
        var code = "004217";

        var hash = hasher.HashOtp(code);

        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length); // 256 bits HMAC-SHA256 = 32 bytes = 64 hex characters
    }

    [Fact]
    public void VerifyHash_CorrectOtpAndCorrectKey_ReturnsTrue()
    {
        var hasher = new HmacSha256OtpHasher(DefaultTestSecret);
        var code = "123456";
        var hash = hasher.HashOtp(code);

        var isValid = hasher.VerifyHash("123456", hash);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyHash_LeadingZeroOtp_VerifiesCorrectly()
    {
        var hasher = new HmacSha256OtpHasher(DefaultTestSecret);
        var code = "000731";
        var hash = hasher.HashOtp(code);

        var isValid = hasher.VerifyHash("000731", hash);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyHash_IncorrectOtp_ReturnsFalse()
    {
        var hasher = new HmacSha256OtpHasher(DefaultTestSecret);
        var hash = hasher.HashOtp("123456");

        var isValid = hasher.VerifyHash("654321", hash);

        Assert.False(isValid);
    }

    [Fact]
    public void VerifyHash_CorrectOtpWithIncorrectKey_ReturnsFalse()
    {
        var hasherKey1 = new HmacSha256OtpHasher(DefaultTestSecret);
        var hasherKey2 = new HmacSha256OtpHasher(AltTestSecret);

        var code = "123456";
        var hashWithKey1 = hasherKey1.HashOtp(code);

        // Verifying hash generated with Key1 using Key2 must fail
        var isValid = hasherKey2.VerifyHash(code, hashWithKey1);

        Assert.False(isValid);
    }

    [Fact]
    public void HashOtp_DifferentSecretKeys_ProduceDifferentVerifiers()
    {
        var hasherKey1 = new HmacSha256OtpHasher(DefaultTestSecret);
        var hasherKey2 = new HmacSha256OtpHasher(AltTestSecret);

        var code = "000731";
        var hash1 = hasherKey1.HashOtp(code);
        var hash2 = hasherKey2.HashOtp(code);

        Assert.NotEqual(hash1, hash2);
    }

    [Theory]
    [InlineData("invalid-hex-string")]
    [InlineData("123")] // Odd length hex
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    public void VerifyHash_MalformedStoredVerifier_ReturnsFalseSafely(string malformedHash)
    {
        var hasher = new HmacSha256OtpHasher(DefaultTestSecret);

        var isValid = hasher.VerifyHash("123456", malformedHash);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HashOtp_EmptyCode_ThrowsArgumentException(string? emptyCode)
    {
        var hasher = new HmacSha256OtpHasher(DefaultTestSecret);
        Assert.Throws<ArgumentException>(() => hasher.HashOtp(emptyCode!));
    }
}
