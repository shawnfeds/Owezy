namespace Owezy.Application.Auth;

public sealed class OtpHasherOptions
{
    public const string Position = "OtpHasher";

    /// <summary>
    /// Server-side secret key used for HMAC-SHA-256 OTP verifier calculation.
    /// </summary>
    public string SecretKey { get; set; } = "owezy-dev-otp-secret-key-32bytes-long-change-in-prod!";
}
