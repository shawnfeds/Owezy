using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Owezy.Application.Common;
using Owezy.Domain.Auth;

namespace Owezy.Application.Auth;

public sealed class JwtAccessTokenService : IAccessTokenService
{
    private readonly JwtOptions _options;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly SymmetricSecurityKey _key;

    public JwtAccessTokenService(JwtOptions options, IDateTimeProvider dateTimeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            throw new InvalidOperationException("JWT signing key is missing. SigningKey must be supplied via configuration.");
        }

        if (options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("JWT signing key must be at least 32 characters (256 bits) long.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            throw new InvalidOperationException("JWT issuer is missing.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException("JWT audience is missing.");
        }

        if (options.AccessTokenLifetimeMinutes <= 0)
        {
            throw new InvalidOperationException("JWT AccessTokenLifetimeMinutes must be greater than zero.");
        }

        _options = options;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
    }

    public AccessTokenResult GenerateAccessToken(PhoneNumber phoneNumber)
    {
        ArgumentNullException.ThrowIfNull(phoneNumber);

        var now = _dateTimeProvider.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, phoneNumber.Value),
            new Claim("phone_number", phoneNumber.Value),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        var encodedToken = tokenHandler.WriteToken(token);

        return new AccessTokenResult(encodedToken, "Bearer", expiresAt);
    }
}
