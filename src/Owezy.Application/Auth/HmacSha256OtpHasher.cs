using System.Security.Cryptography;
using System.Text;

namespace Owezy.Application.Auth;

public sealed class HmacSha256OtpHasher : IOtpHasher
{
    private readonly byte[] _keyBytes;

    public HmacSha256OtpHasher(OtpHasherOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            throw new InvalidOperationException("OTP HMAC secret key is missing. SecretKey must be supplied via configuration.");
        }

        _keyBytes = Encoding.UTF8.GetBytes(options.SecretKey);
    }

    public HmacSha256OtpHasher(string secretKey)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("OTP HMAC secret key is missing. SecretKey must be supplied.");
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

        byte[] storedBytes;
        try
        {
            storedBytes = Convert.FromHexString(hash);
        }
        catch (FormatException)
        {
            return false;
        }

        var computedBytes = ComputeHashBytes(otpCode);

        return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }

    private byte[] ComputeHashBytes(string otpCode)
    {
        var otpBytes = Encoding.UTF8.GetBytes(otpCode);
        return HMACSHA256.HashData(_keyBytes, otpBytes);
    }
}
