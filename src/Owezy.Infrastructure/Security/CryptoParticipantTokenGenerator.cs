using System.Security.Cryptography;
using System.Text;
using Owezy.Application.Billing;

namespace Owezy.Infrastructure.Security;

public sealed class CryptoParticipantTokenGenerator : IParticipantTokenGenerator
{
    public string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexStringLower(bytes);
    }

    public string HashToken(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new ArgumentException("Token cannot be null or empty.", nameof(rawToken));
        }

        var bytes = Encoding.UTF8.GetBytes(rawToken.Trim());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
