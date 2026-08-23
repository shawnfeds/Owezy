namespace Owezy.Domain.Auth;

public sealed class OtpChallenge
{
    public const int DefaultValidityMinutes = 5;
    public const int DefaultMaxAttempts = 5;

    public OtpChallengeId Id { get; }
    public PhoneNumber PhoneNumber { get; }
    public string OtpHash { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public int RemainingAttempts { get; private set; }
    public OtpState State { get; private set; }

    private OtpChallenge(
        OtpChallengeId id,
        PhoneNumber phoneNumber,
        string otpHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        int remainingAttempts,
        OtpState state)
    {
        Id = id;
        PhoneNumber = phoneNumber ?? throw new ArgumentNullException(nameof(phoneNumber));
        OtpHash = !string.IsNullOrWhiteSpace(otpHash) ? otpHash : throw new ArgumentException("OTP hash cannot be empty.", nameof(otpHash));
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        RemainingAttempts = remainingAttempts;
        State = state;
    }

    public static OtpChallenge Create(PhoneNumber phoneNumber, string otpHash, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(phoneNumber);
        if (string.IsNullOrWhiteSpace(otpHash))
        {
            throw new ArgumentException("OTP hash cannot be empty.", nameof(otpHash));
        }

        var expiresAt = now.AddMinutes(DefaultValidityMinutes);

        return new OtpChallenge(
            OtpChallengeId.New(),
            phoneNumber,
            otpHash,
            now,
            expiresAt,
            DefaultMaxAttempts,
            OtpState.Active
        );
    }

    public static OtpChallenge Reconstitute(
        OtpChallengeId id,
        PhoneNumber phoneNumber,
        string otpHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        int remainingAttempts,
        OtpState state)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Challenge ID cannot be empty.", nameof(id));
        }

        return new OtpChallenge(id, phoneNumber, otpHash, createdAt, expiresAt, remainingAttempts, state);
    }

    public void Expire()
    {
        State = OtpState.Expired;
    }

    public OtpVerificationResult Verify(bool isHashMatch, DateTimeOffset now)
    {
        if (State == OtpState.Verified)
        {
            return OtpVerificationResult.AlreadyCompleted;
        }

        if (State == OtpState.Exhausted || RemainingAttempts <= 0)
        {
            State = OtpState.Exhausted;
            return OtpVerificationResult.Exhausted;
        }

        if (State == OtpState.Expired || now >= ExpiresAt)
        {
            State = OtpState.Expired;
            return OtpVerificationResult.Expired;
        }

        if (isHashMatch)
        {
            State = OtpState.Verified;
            return OtpVerificationResult.Success;
        }

        RemainingAttempts--;

        if (RemainingAttempts <= 0)
        {
            State = OtpState.Exhausted;
            return OtpVerificationResult.Exhausted;
        }

        return OtpVerificationResult.InvalidOtp;
    }
}
