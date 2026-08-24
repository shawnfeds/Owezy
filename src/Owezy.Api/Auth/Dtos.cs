namespace Owezy.Api.Auth;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed record RequestOtpHttpRequest(string? PhoneNumber);

public sealed record VerifyOtpHttpRequest(string? ChallengeId, string? Otp);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public sealed record RequestOtpHttpResponse(string ChallengeId);

public sealed record VerifyOtpHttpResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);

public sealed record ApiError(string Code, string Message);
