using Owezy.Domain.Auth;

namespace Owezy.Application.Auth;

public sealed record RequestOtpResult
{
    public bool IsSuccess { get; }
    public OtpChallengeId? ChallengeId { get; }
    public string? ErrorMessage { get; }

    private RequestOtpResult(bool isSuccess, OtpChallengeId? challengeId, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ChallengeId = challengeId;
        ErrorMessage = errorMessage;
    }

    public static RequestOtpResult Success(OtpChallengeId challengeId) =>
        new(true, challengeId, null);

    public static RequestOtpResult Failure(string errorMessage) =>
        new(false, null, errorMessage);
}
