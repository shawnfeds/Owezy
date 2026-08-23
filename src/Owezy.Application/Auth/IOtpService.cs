using Owezy.Domain.Auth;

namespace Owezy.Application.Auth;

public readonly record struct CreateChallengeResult(
    OtpChallengeId ChallengeId,
    DateTimeOffset ExpiresAt,
    int RemainingAttempts
);

public interface IOtpService
{
    Task<CreateChallengeResult> CreateChallengeAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken = default);
    Task<OtpVerificationResult> VerifyChallengeAsync(OtpChallengeId challengeId, string otpCode, CancellationToken cancellationToken = default);
}
