using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Owezy.Application.Auth;
using Owezy.Application.Common;
using Owezy.Domain.Auth;
using Xunit;

namespace Owezy.UnitTests.Auth;

public class JwtAccessTokenServiceTests
{
    private class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private const string TestSigningKey = "test-jwt-signing-secret-key-32chars-long-12345";
    private readonly TestDateTimeProvider _dateTimeProvider = new();
    private readonly PhoneNumber _testPhone = PhoneNumber.Create("+919876543210");

    private JwtOptions CreateValidOptions() => new()
    {
        SigningKey = TestSigningKey,
        Issuer = "Owezy.Api.Test",
        Audience = "Owezy.App.Test",
        AccessTokenLifetimeMinutes = 15
    };

    [Fact]
    public void GenerateAccessToken_ValidInput_ReturnsTokenWithCorrectProperties()
    {
        var options = CreateValidOptions();
        var service = new JwtAccessTokenService(options, _dateTimeProvider);

        var result = service.GenerateAccessToken(_testPhone);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal("Bearer", result.TokenType);

        // Decode and inspect claims
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.AccessToken);

        Assert.Equal("Owezy.Api.Test", jwt.Issuer);
        Assert.Contains("Owezy.App.Test", jwt.Audiences);
        Assert.Equal(_testPhone.Value, jwt.Subject);

        var phoneClaim = jwt.Claims.FirstOrDefault(c => c.Type == "phone_number");
        Assert.NotNull(phoneClaim);
        Assert.Equal(_testPhone.Value, phoneClaim.Value);

        // Security assertions: Token MUST NOT contain sensitive authentication internals
        Assert.DoesNotContain(jwt.Claims, c => c.Type.Contains("otp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(jwt.Claims, c => c.Type.Contains("hash", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(jwt.Claims, c => c.Type.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(jwt.Claims, c => c.Type.Contains("hmac", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GenerateAccessToken_TokenSignatureAndClaims_CanBeValidated()
    {
        var options = CreateValidOptions();
        var service = new JwtAccessTokenService(options, _dateTimeProvider);

        var result = service.GenerateAccessToken(_testPhone);

        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5)
        };

        var principal = tokenHandler.ValidateToken(result.AccessToken, validationParameters, out var validatedToken);

        Assert.NotNull(principal);
        Assert.NotNull(validatedToken);

        var subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
        Assert.Equal(_testPhone.Value, subClaim);
    }

    [Fact]
    public void ValidateToken_WrongSigningKey_ThrowsSecurityTokenValidationException()
    {
        var options = CreateValidOptions();
        var service = new JwtAccessTokenService(options, _dateTimeProvider);
        var result = service.GenerateAccessToken(_testPhone);

        var tokenHandler = new JwtSecurityTokenHandler();
        var wrongKeyValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("wrong-key-secret-32-characters-long-00000")),
            ValidateLifetime = false
        };

        Assert.ThrowsAny<SecurityTokenValidationException>(() =>
            tokenHandler.ValidateToken(result.AccessToken, wrongKeyValidationParameters, out _));
    }

    [Fact]
    public void ValidateToken_WrongIssuer_ThrowsSecurityTokenInvalidIssuerException()
    {
        var options = CreateValidOptions();
        var service = new JwtAccessTokenService(options, _dateTimeProvider);
        var result = service.GenerateAccessToken(_testPhone);

        var tokenHandler = new JwtSecurityTokenHandler();
        var wrongIssuerParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "Wrong.Issuer",
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            ValidateLifetime = false
        };

        Assert.Throws<SecurityTokenInvalidIssuerException>(() =>
            tokenHandler.ValidateToken(result.AccessToken, wrongIssuerParameters, out _));
    }

    [Fact]
    public void ValidateToken_WrongAudience_ThrowsSecurityTokenInvalidAudienceException()
    {
        var options = CreateValidOptions();
        var service = new JwtAccessTokenService(options, _dateTimeProvider);
        var result = service.GenerateAccessToken(_testPhone);

        var tokenHandler = new JwtSecurityTokenHandler();
        var wrongAudienceParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = "Wrong.Audience",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            ValidateLifetime = false
        };

        Assert.Throws<SecurityTokenInvalidAudienceException>(() =>
            tokenHandler.ValidateToken(result.AccessToken, wrongAudienceParameters, out _));
    }

    [Fact]
    public void ValidateToken_ExpiredToken_ThrowsSecurityTokenExpiredException()
    {
        var options = CreateValidOptions();
        var pastTimeProvider = new TestDateTimeProvider { UtcNow = DateTimeOffset.UtcNow.AddHours(-2) };
        var service = new JwtAccessTokenService(options, pastTimeProvider);
        var result = service.GenerateAccessToken(_testPhone);

        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        Assert.Throws<SecurityTokenExpiredException>(() =>
            tokenHandler.ValidateToken(result.AccessToken, validationParameters, out _));
    }

    [Fact]
    public void Constructor_MissingSigningKey_ThrowsInvalidOperationException()
    {
        var options = new JwtOptions { SigningKey = "" };
        Assert.Throws<InvalidOperationException>(() => new JwtAccessTokenService(options, _dateTimeProvider));
    }

    [Fact]
    public void Constructor_SigningKeyTooShort_ThrowsInvalidOperationException()
    {
        var options = new JwtOptions { SigningKey = "short-key" };
        Assert.Throws<InvalidOperationException>(() => new JwtAccessTokenService(options, _dateTimeProvider));
    }
}
