using System.Security.Cryptography;
using System.Text;

namespace Owezy.Application.Auth;

public sealed class Sha256OtpHasher : IOtpHasher
{
    private readonly byte[] _keyBytes;

    public Sha256OtpHasher(OtpHasherOptions? options = null)
    {
        var secret = options?.SecretKey;
        if (string.IsNullOrWhiteSpace(secret))
        {
            secret = new OtpHasherOptions().SecretKey;
        }

        _keyBytes = Encoding.UTF8.GetBytes(secret);
    }

    public Sha256OtpHasher(string secretKey)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new ArgumentException("Secret key cannot be empty.", nameof(secretKey));
        }

        _keyBytes = Encoding.UTF8.GetBytes(secretKey);
    }

    public string HashOtp(string otpCode)
    {
        if (string.IsNullOrWhiteSpace(otpCode))
        {
            throw new ArgumentException("OTP code cannot be empty.", nameof(otpCode));
        }

        var otpBytes = Encoding.UTF8.GetBytes(otpCode);
        var hashBytes = HMACSHA256.HashData(_keyBytes, otpBytes);
        return Convert.ToHexString(hashBytes);
    }

    public bool VerifyHash(string otpCode, string hash)
    {
        if (string.IsNullOrWhiteSpace(otpCode) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        var computedHash = HashOtp(otpCode);
        return string.Equals(computedHash, hash, StringComparison.OrdinalIgnoreCase);
    }
}
