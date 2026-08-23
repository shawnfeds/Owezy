namespace Owezy.Application.Auth;

public interface IOtpHasher
{
    string HashOtp(string otpCode);
    bool VerifyHash(string otpCode, string hash);
}
