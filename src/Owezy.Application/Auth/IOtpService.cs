using Owezy.Domain.Auth;

namespace Owezy.Application.Auth;

public interface IOtpService
{
    Task<RequestOtpResult> RequestOtpAsync(RequestOtpRequest request, CancellationToken cancellationToken = default);
    Task<VerifyOtpResult> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);

    Task<RequestOtpResult> RequestOtpAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken = default);
    Task<VerifyOtpResult> VerifyOtpAsync(OtpChallengeId challengeId, string otpCode, CancellationToken cancellationToken = default);
}
