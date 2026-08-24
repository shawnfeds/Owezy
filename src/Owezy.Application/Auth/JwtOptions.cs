namespace Owezy.Application.Auth;

public sealed class JwtOptions
{
    public const string Position = "Jwt";

    /// <summary>
    /// Server-side secret key used for signing JWT access tokens (HMAC-SHA256).
    /// MUST be at least 32 characters (256 bits) long and supplied via external configuration.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Token issuer. Defaults to "Owezy.Api".
    /// </summary>
    public string Issuer { get; set; } = "Owezy.Api";

    /// <summary>
    /// Token audience. Defaults to "Owezy.App".
    /// </summary>
    public string Audience { get; set; } = "Owezy.App";

    /// <summary>
    /// Lifetime of access token in minutes. Defaults to 15.
    /// </summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
}
