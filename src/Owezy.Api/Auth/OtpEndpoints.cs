using Owezy.Application.Auth;
using Owezy.Domain.Auth;

namespace Owezy.Api.Auth;

public static class OtpEndpoints
{
    public static IEndpointRouteBuilder MapOtpEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth/otp");

        group.MapPost("/request", HandleRequestOtpAsync)
            .WithName("RequestOtp");

        group.MapPost("/verify", HandleVerifyOtpAsync)
            .WithName("VerifyOtp");

        return app;
    }

    private static async Task<IResult> HandleRequestOtpAsync(
        RequestOtpHttpRequest httpRequest,
        IOtpService otpService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(httpRequest.PhoneNumber))
        {
            return Results.BadRequest(new ApiError("invalid_request", "phoneNumber is required."));
        }

        PhoneNumber phoneNumber;
        try
        {
            phoneNumber = PhoneNumber.Create(httpRequest.PhoneNumber);
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new ApiError("invalid_phone_number", "The provided phone number is not valid. Use E.164 format (e.g. +919876543210)."));
        }

        RequestOtpResult result;
        try
        {
            result = await otpService.RequestOtpAsync(phoneNumber, cancellationToken);
        }
        catch (Exception)
        {
            return Results.Problem("An unexpected error occurred. Please try again.", statusCode: 500);
        }

        if (!result.IsSuccess)
        {
            return Results.Problem("OTP could not be sent. Please try again.", statusCode: 502);
        }

        return Results.Accepted(value: new RequestOtpHttpResponse(result.ChallengeId!.Value.Value.ToString()));
    }

    private static async Task<IResult> HandleVerifyOtpAsync(
        VerifyOtpHttpRequest httpRequest,
        IOtpService otpService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(httpRequest.ChallengeId))
        {
            return Results.BadRequest(new ApiError("invalid_request", "challengeId is required."));
        }

        if (string.IsNullOrWhiteSpace(httpRequest.Otp))
        {
            return Results.BadRequest(new ApiError("invalid_request", "otp is required."));
        }

        if (!Guid.TryParse(httpRequest.ChallengeId, out var challengeGuid))
        {
            return Results.BadRequest(new ApiError("invalid_challenge_id", "challengeId must be a valid GUID."));
        }

        if (httpRequest.Otp.Length != 6 || !httpRequest.Otp.All(char.IsDigit))
        {
            return Results.BadRequest(new ApiError("invalid_otp_format", "otp must be a 6-digit numeric code."));
        }

        var challengeId = new OtpChallengeId(challengeGuid);

        VerifyOtpResult result;
        try
        {
            result = await otpService.VerifyOtpAsync(challengeId, httpRequest.Otp, cancellationToken);
        }
        catch (Exception)
        {
            return Results.Problem("An unexpected error occurred. Please try again.", statusCode: 500);
        }

        return result.Status switch
        {
            Domain.Auth.OtpVerificationResult.Success =>
                Results.Ok(new VerifyOtpHttpResponse(result.AuthenticatedPhoneNumber!.Value)),

            Domain.Auth.OtpVerificationResult.ChallengeNotFound =>
                Results.NotFound(new ApiError("challenge_not_found", "The OTP challenge was not found.")),

            Domain.Auth.OtpVerificationResult.Expired =>
                Results.UnprocessableEntity(new ApiError("otp_expired", "The OTP has expired. Please request a new one.")),

            Domain.Auth.OtpVerificationResult.Exhausted =>
                Results.Json(new ApiError("otp_exhausted", "Too many failed attempts. Please request a new OTP."), statusCode: 409),

            Domain.Auth.OtpVerificationResult.AlreadyCompleted =>
                Results.Json(new ApiError("challenge_already_used", "This OTP challenge has already been used."), statusCode: 409),

            Domain.Auth.OtpVerificationResult.InvalidOtp =>
                Results.Json(new ApiError("invalid_otp", "The OTP is incorrect."), statusCode: 401),

            _ =>
                Results.Problem("An unexpected error occurred.", statusCode: 500)
        };
    }
}
