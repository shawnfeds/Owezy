using Owezy.Application.Common;
using Owezy.Domain.Auth;

namespace Owezy.Application.Auth;

public sealed class OtpService : IOtpService
{
    private readonly IOtpChallengeRepository _repository;
    private readonly IOtpGenerator _otpGenerator;
    private readonly IOtpHasher _otpHasher;
    private readonly ISmsProvider _smsProvider;
    private readonly IDateTimeProvider _dateTimeProvider;

    public OtpService(
        IOtpChallengeRepository repository,
        IOtpGenerator otpGenerator,
        IOtpHasher otpHasher,
        ISmsProvider smsProvider,
        IDateTimeProvider dateTimeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _otpGenerator = otpGenerator ?? throw new ArgumentNullException(nameof(otpGenerator));
        _otpHasher = otpHasher ?? throw new ArgumentNullException(nameof(otpHasher));
        _smsProvider = smsProvider ?? throw new ArgumentNullException(nameof(smsProvider));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
    }

    public Task<RequestOtpResult> RequestOtpAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken = default)
    {
        return RequestOtpAsync(new RequestOtpRequest(phoneNumber), cancellationToken);
    }

    public async Task<RequestOtpResult> RequestOtpAsync(RequestOtpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.PhoneNumber);

        var now = _dateTimeProvider.UtcNow;
        var rawOtp = _otpGenerator.GenerateOtp();
        var hash = _otpHasher.HashOtp(rawOtp);

        var challenge = OtpChallenge.Create(request.PhoneNumber, hash, now);

        await _repository.AddAsync(challenge, cancellationToken);

        try
        {
            var message = $"Your Owezy verification code is: {rawOtp}. Valid for 5 minutes.";
            await _smsProvider.SendSmsAsync(request.PhoneNumber, message, cancellationToken);
            return RequestOtpResult.Success(challenge.Id);
        }
        catch (Exception)
        {
            challenge.Expire();
            try
            {
                await _repository.UpdateAsync(challenge, cancellationToken);
            }
            catch
            {
                // Best effort cleanup if SMS provider fails
            }

            return RequestOtpResult.Failure("SMS delivery failed. OTP challenge was invalidated.");
        }
    }

    public Task<VerifyOtpResult> VerifyOtpAsync(OtpChallengeId challengeId, string otpCode, CancellationToken cancellationToken = default)
    {
        return VerifyOtpAsync(new VerifyOtpRequest(challengeId, otpCode), cancellationToken);
    }

    public async Task<VerifyOtpResult> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ChallengeId.Value == Guid.Empty || string.IsNullOrWhiteSpace(request.OtpCode))
        {
            return VerifyOtpResult.Failure(OtpVerificationResult.ChallengeNotFound);
        }

        var challenge = await _repository.GetByIdAsync(request.ChallengeId, cancellationToken);
        if (challenge is null)
        {
            return VerifyOtpResult.Failure(OtpVerificationResult.ChallengeNotFound);
        }

        var now = _dateTimeProvider.UtcNow;
        var isMatch = _otpHasher.VerifyHash(request.OtpCode, challenge.OtpHash);

        var domainResult = challenge.Verify(isMatch, now);

        try
        {
            await _repository.UpdateAsync(challenge, cancellationToken);
        }
        catch (Exception)
        {
            return VerifyOtpResult.Failure(OtpVerificationResult.Exhausted);
        }

        if (domainResult == OtpVerificationResult.Success)
        {
            return VerifyOtpResult.Success(challenge.PhoneNumber);
        }

        return VerifyOtpResult.Failure(domainResult);
    }
}
