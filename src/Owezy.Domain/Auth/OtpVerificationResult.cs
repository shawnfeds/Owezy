namespace Owezy.Domain.Auth;

public enum OtpVerificationResult
{
    Success = 1,
    InvalidOtp = 2,
    Expired = 3,
    Exhausted = 4,
    AlreadyCompleted = 5,
    ChallengeNotFound = 6
}
