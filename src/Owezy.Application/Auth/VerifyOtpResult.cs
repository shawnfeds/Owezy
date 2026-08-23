using Owezy.Domain.Auth;

namespace Owezy.Application.Auth;

public sealed record VerifyOtpResult
{
    public OtpVerificationResult Status { get; }
    public PhoneNumber? AuthenticatedPhoneNumber { get; }

    public bool IsSuccess => Status == OtpVerificationResult.Success;

    private VerifyOtpResult(OtpVerificationResult status, PhoneNumber? authenticatedPhoneNumber)
    {
        Status = status;
        AuthenticatedPhoneNumber = authenticatedPhoneNumber;
    }

    public static VerifyOtpResult Success(PhoneNumber phoneNumber) =>
        new(OtpVerificationResult.Success, phoneNumber);

    public static VerifyOtpResult Failure(OtpVerificationResult status) =>
        new(status, null);
}
