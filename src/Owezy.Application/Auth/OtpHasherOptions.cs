namespace Owezy.Application.Auth;

public sealed class OtpHasherOptions
{
    public const string Position = "OtpHasher";

    /// <summary>
    /// Server-side secret key used for HMAC-SHA-256 OTP verifier calculation.
    /// MUST be supplied via configuration / dependency injection.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;
}
