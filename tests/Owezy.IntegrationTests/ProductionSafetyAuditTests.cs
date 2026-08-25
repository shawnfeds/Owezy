using Owezy.Application.Auth;
using Owezy.Application.Common;
using Owezy.Infrastructure.Storage;
using Xunit;

namespace Owezy.IntegrationTests;

public class ProductionSafetyAuditTests
{
    private class DummyDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    [Fact]
    public void OtpHasher_MissingSecretKey_ThrowsInvalidOperationException()
    {
        var options = new OtpHasherOptions { SecretKey = "" };
        Assert.Throws<InvalidOperationException>(() => new HmacSha256OtpHasher(options));
    }

    [Fact]
    public void JwtAccessTokenService_MissingSigningKey_ThrowsInvalidOperationException()
    {
        var options = new JwtOptions
        {
            SigningKey = "",
            Issuer = "Owezy.Api",
            Audience = "Owezy.App",
            AccessTokenLifetimeMinutes = 15
        };

        Assert.Throws<InvalidOperationException>(() => new JwtAccessTokenService(options, new DummyDateTimeProvider()));
    }

    [Fact]
    public void JwtAccessTokenService_ShortSigningKey_ThrowsInvalidOperationException()
    {
        var options = new JwtOptions
        {
            SigningKey = "short-key-under-32-chars",
            Issuer = "Owezy.Api",
            Audience = "Owezy.App",
            AccessTokenLifetimeMinutes = 15
        };

        Assert.Throws<InvalidOperationException>(() => new JwtAccessTokenService(options, new DummyDateTimeProvider()));
    }

    [Fact]
    public async Task ReceiptStorage_PathTraversalExtension_SanitizesKeySafely()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "OwezySafetyTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalFileReceiptStorage(tempDir);
            using var ms = new MemoryStream("fake-image"u8.ToArray());

            // Attempt extension with path traversal sequence
            var key = await storage.StoreAsync(ms, "../../../etc/passwd.jpg");

            Assert.NotNull(key);
            Assert.DoesNotContain("..", key);
            Assert.DoesNotContain("/", key);
            Assert.DoesNotContain("\\", key);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
