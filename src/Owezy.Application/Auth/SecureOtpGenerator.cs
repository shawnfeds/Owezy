using System.Security.Cryptography;

namespace Owezy.Application.Auth;

public sealed class SecureOtpGenerator : IOtpGenerator
{
    public string GenerateOtp()
    {
        var number = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return number.ToString("D6");
    }
}
