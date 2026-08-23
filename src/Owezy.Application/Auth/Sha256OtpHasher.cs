using System.Security.Cryptography;
using System.Text;

namespace Owezy.Application.Auth;

public sealed class Sha256OtpHasher : IOtpHasher
{
    public string HashOtp(string otpCode)
    {
        if (string.IsNullOrWhiteSpace(otpCode))
        {
            throw new ArgumentException("OTP code cannot be empty.", nameof(otpCode));
        }

        var bytes = Encoding.UTF8.GetBytes(otpCode);
        var hashBytes = SHA256.HashData(bytes);
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
