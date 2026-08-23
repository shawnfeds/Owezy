using Owezy.Domain.Auth;
using Xunit;

namespace Owezy.UnitTests.Auth;

public class OtpChallengeTests
{
    private readonly PhoneNumber _testPhone = PhoneNumber.Create("+919876543210");
    private const string DummyHash = "8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92";

    [Fact]
    public void Create_InitializesStateWithFiveMinutesExpiryAndFiveAttempts()
    {
        var now = new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);

        var challenge = OtpChallenge.Create(_testPhone, DummyHash, now);

        Assert.NotNull(challenge);
        Assert.NotEqual(Guid.Empty, challenge.Id.Value);
        Assert.Equal(_testPhone, challenge.PhoneNumber);
        Assert.Equal(DummyHash, challenge.OtpHash);
        Assert.Equal(now, challenge.CreatedAt);
        Assert.Equal(now.AddMinutes(5), challenge.ExpiresAt);
        Assert.Equal(5, challenge.RemainingAttempts);
        Assert.Equal(OtpState.Active, challenge.State);
    }

    [Fact]
    public void Verify_ValidHashBeforeExpiry_ReturnsSuccessAndStateBecomesVerified()
    {
        var now = new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);
        var challenge = OtpChallenge.Create(_testPhone, DummyHash, now);

        var verifyTime = now.AddMinutes(4);
        var result = challenge.Verify(isHashMatch: true, verifyTime);

        Assert.Equal(OtpVerificationResult.Success, result);
        Assert.Equal(OtpState.Verified, challenge.State);
    }

    [Fact]
    public void Verify_ExactFiveMinuteBoundary_IsExpiredAtOrAfterBoundary()
    {
        var createdAt = new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);
        var challenge = OtpChallenge.Create(_testPhone, DummyHash, createdAt);

        // Before 5 minutes: active
        var beforeExpiry = createdAt.AddMinutes(4).AddSeconds(59);
        var validResult = challenge.Verify(isHashMatch: true, beforeExpiry);
        Assert.Equal(OtpVerificationResult.Success, validResult);

        // Reset new challenge for boundary check
        var challenge2 = OtpChallenge.Create(_testPhone, DummyHash, createdAt);
        var exactBoundary = createdAt.AddMinutes(5); // 10:05:00
        var expiredResult = challenge2.Verify(isHashMatch: true, exactBoundary);

        Assert.Equal(OtpVerificationResult.Expired, expiredResult);
        Assert.Equal(OtpState.Expired, challenge2.State);
    }

    [Fact]
    public void Verify_IncorrectHash_DecrementsAttempts()
    {
        var now = new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);
        var challenge = OtpChallenge.Create(_testPhone, DummyHash, now);

        var result = challenge.Verify(isHashMatch: false, now.AddMinutes(1));

        Assert.Equal(OtpVerificationResult.InvalidOtp, result);
        Assert.Equal(4, challenge.RemainingAttempts);
        Assert.Equal(OtpState.Active, challenge.State);
    }

    [Fact]
    public void Verify_FiveIncorrectAttempts_ExhaustsChallenge()
    {
        var now = new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);
        var challenge = OtpChallenge.Create(_testPhone, DummyHash, now);

        for (int i = 0; i < 4; i++)
        {
            var res = challenge.Verify(isHashMatch: false, now.AddSeconds(i * 10));
            Assert.Equal(OtpVerificationResult.InvalidOtp, res);
        }

        Assert.Equal(1, challenge.RemainingAttempts);

        // 5th failed attempt -> Exhausted
        var fifthRes = challenge.Verify(isHashMatch: false, now.AddSeconds(50));
        Assert.Equal(OtpVerificationResult.Exhausted, fifthRes);
        Assert.Equal(0, challenge.RemainingAttempts);
        Assert.Equal(OtpState.Exhausted, challenge.State);

        // 6th attempt -> Exiting exhausted state stays exhausted
        var sixthRes = challenge.Verify(isHashMatch: true, now.AddSeconds(60));
        Assert.Equal(OtpVerificationResult.Exhausted, sixthRes);
    }

    [Fact]
    public void Verify_AlreadyVerifiedChallenge_ReturnsAlreadyCompleted()
    {
        var now = new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);
        var challenge = OtpChallenge.Create(_testPhone, DummyHash, now);

        challenge.Verify(isHashMatch: true, now.AddMinutes(1));
        Assert.Equal(OtpState.Verified, challenge.State);

        var secondResult = challenge.Verify(isHashMatch: true, now.AddMinutes(2));
        Assert.Equal(OtpVerificationResult.AlreadyCompleted, secondResult);
    }
}
