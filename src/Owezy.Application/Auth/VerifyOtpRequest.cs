using Owezy.Domain.Auth;

namespace Owezy.Application.Auth;

public sealed record VerifyOtpRequest(OtpChallengeId ChallengeId, string OtpCode);
