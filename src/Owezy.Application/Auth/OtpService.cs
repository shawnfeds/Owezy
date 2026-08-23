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

    public async Task<CreateChallengeResult> CreateChallengeAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(phoneNumber);

        var now = _dateTimeProvider.UtcNow;
        var rawOtp = _otpGenerator.GenerateOtp();
        var hash = _otpHasher.HashOtp(rawOtp);

        var challenge = OtpChallenge.Create(phoneNumber, hash, now);

        await _repository.AddAsync(challenge, cancellationToken);

        var message = $"Your Owezy verification code is: {rawOtp}. Valid for 5 minutes.";
        await _smsProvider.SendSmsAsync(phoneNumber, message, cancellationToken);

        return new CreateChallengeResult(challenge.Id, challenge.ExpiresAt, challenge.RemainingAttempts);
    }

    public async Task<OtpVerificationResult> VerifyChallengeAsync(OtpChallengeId challengeId, string otpCode, CancellationToken cancellationToken = default)
    {
        if (challengeId.Value == Guid.Empty || string.IsNullOrWhiteSpace(otpCode))
        {
            return OtpVerificationResult.ChallengeNotFound;
        }

        var challenge = await _repository.GetByIdAsync(challengeId, cancellationToken);
        if (challenge is null)
        {
            return OtpVerificationResult.ChallengeNotFound;
        }

        var now = _dateTimeProvider.UtcNow;
        var isMatch = _otpHasher.VerifyHash(otpCode, challenge.OtpHash);

        var result = challenge.Verify(isMatch, now);

        await _repository.UpdateAsync(challenge, cancellationToken);

        return result;
    }
}
