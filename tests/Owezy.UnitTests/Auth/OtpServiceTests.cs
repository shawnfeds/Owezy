using Owezy.Application.Auth;
using Owezy.Application.Common;
using Owezy.Domain.Auth;
using Xunit;

namespace Owezy.UnitTests.Auth;

public class OtpServiceTests
{
    private class InMemoryOtpChallengeRepository : IOtpChallengeRepository
    {
        public Dictionary<OtpChallengeId, OtpChallenge> Store { get; } = new();

        public Task<OtpChallenge?> GetByIdAsync(OtpChallengeId id, CancellationToken cancellationToken = default)
        {
            Store.TryGetValue(id, out var challenge);
            return Task.FromResult(challenge);
        }

        public Task AddAsync(OtpChallenge challenge, CancellationToken cancellationToken = default)
        {
            Store[challenge.Id] = challenge;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(OtpChallenge challenge, CancellationToken cancellationToken = default)
        {
            Store[challenge.Id] = challenge;
            return Task.CompletedTask;
        }
    }

    private class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);
    }

    private class FailingSmsProvider : ISmsProvider
    {
        public Task SendSmsAsync(PhoneNumber recipient, string message, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("SMS Gateway Connection Failed.");
        }
    }

    private readonly InMemoryOtpChallengeRepository _repository = new();
    private readonly SecureOtpGenerator _otpGenerator = new();
    private readonly HmacSha256OtpHasher _otpHasher = new("test-service-secret-key-1234567890");
    private readonly DevelopmentSmsProvider _smsProvider = new();
    private readonly TestDateTimeProvider _dateTimeProvider = new();
    private readonly PhoneNumber _testPhone = PhoneNumber.Create("+919876543210");

    [Fact]
    public async Task RequestOtpAsync_ValidPhoneNumber_CreatesChallengePersistsAndSendsSms()
    {
        var service = new OtpService(_repository, _otpGenerator, _otpHasher, _smsProvider, _dateTimeProvider);

        var result = await service.RequestOtpAsync(_testPhone);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ChallengeId);
        Assert.NotEqual(Guid.Empty, result.ChallengeId.Value.Value);

        // Verify persisted
        var persisted = await _repository.GetByIdAsync(result.ChallengeId.Value);
        Assert.NotNull(persisted);
        Assert.Equal(_testPhone, persisted.PhoneNumber);

        // Verify SMS sent
        var sentMessages = _smsProvider.GetSentMessages();
        Assert.Single(sentMessages);
        Assert.Equal(_testPhone, sentMessages.First().Recipient);
        Assert.Contains("Your Owezy verification code is:", sentMessages.First().Message);
    }

    [Fact]
    public async Task RequestOtpAsync_ResultDoesNotContainOtp()
    {
        var service = new OtpService(_repository, _otpGenerator, _otpHasher, _smsProvider, _dateTimeProvider);

        var result = await service.RequestOtpAsync(_testPhone);

        // Assert type structure has no OTP field
        Assert.Null(result.ErrorMessage);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RequestOtpAsync_SmsFailure_InvalidatesChallengeAndReturnsFailure()
    {
        var failingSmsProvider = new FailingSmsProvider();
        var service = new OtpService(_repository, _otpGenerator, _otpHasher, failingSmsProvider, _dateTimeProvider);

        var result = await service.RequestOtpAsync(_testPhone);

        Assert.False(result.IsSuccess);
        Assert.Null(result.ChallengeId);
        Assert.Contains("SMS delivery failed", result.ErrorMessage);

        // Verify challenge in repo was expired
        var storedChallenge = _repository.Store.Values.FirstOrDefault();
        Assert.NotNull(storedChallenge);
        Assert.Equal(OtpState.Expired, storedChallenge.State);
    }

    [Fact]
    public async Task VerifyOtpAsync_ValidOtp_ReturnsSuccessAndExposesCanonicalPhoneNumber()
    {
        var service = new OtpService(_repository, _otpGenerator, _otpHasher, _smsProvider, _dateTimeProvider);
        var requestResult = await service.RequestOtpAsync(_testPhone);

        var sentMessage = _smsProvider.GetSentMessages().First().Message;
        var otpCode = sentMessage.Split(':')[1].Trim().Substring(0, 6);

        var verifyResult = await service.VerifyOtpAsync(requestResult.ChallengeId!.Value, otpCode);

        Assert.True(verifyResult.IsSuccess);
        Assert.Equal(OtpVerificationResult.Success, verifyResult.Status);
        Assert.Equal(_testPhone, verifyResult.AuthenticatedPhoneNumber);

        // Verify state was persisted as Verified
        var updatedChallenge = await _repository.GetByIdAsync(requestResult.ChallengeId.Value);
        Assert.NotNull(updatedChallenge);
        Assert.Equal(OtpState.Verified, updatedChallenge.State);
    }

    [Fact]
    public async Task VerifyOtpAsync_InvalidOtp_ConsumesAttemptAndPersistsState()
    {
        var service = new OtpService(_repository, _otpGenerator, _otpHasher, _smsProvider, _dateTimeProvider);
        var requestResult = await service.RequestOtpAsync(_testPhone);

        var verifyResult = await service.VerifyOtpAsync(requestResult.ChallengeId!.Value, "000000");

        Assert.False(verifyResult.IsSuccess);
        Assert.Equal(OtpVerificationResult.InvalidOtp, verifyResult.Status);
        Assert.Null(verifyResult.AuthenticatedPhoneNumber);

        var updatedChallenge = await _repository.GetByIdAsync(requestResult.ChallengeId.Value);
        Assert.NotNull(updatedChallenge);
        Assert.Equal(4, updatedChallenge.RemainingAttempts);
    }

    [Fact]
    public async Task VerifyOtpAsync_FifthInvalidOtp_ExhaustsChallenge()
    {
        var service = new OtpService(_repository, _otpGenerator, _otpHasher, _smsProvider, _dateTimeProvider);
        var requestResult = await service.RequestOtpAsync(_testPhone);

        for (int i = 0; i < 4; i++)
        {
            await service.VerifyOtpAsync(requestResult.ChallengeId!.Value, "000000");
        }

        var fifthResult = await service.VerifyOtpAsync(requestResult.ChallengeId!.Value, "000000");

        Assert.False(fifthResult.IsSuccess);
        Assert.Equal(OtpVerificationResult.Exhausted, fifthResult.Status);

        var updatedChallenge = await _repository.GetByIdAsync(requestResult.ChallengeId.Value);
        Assert.NotNull(updatedChallenge);
        Assert.Equal(OtpState.Exhausted, updatedChallenge.State);
        Assert.Equal(0, updatedChallenge.RemainingAttempts);
    }

    [Fact]
    public async Task VerifyOtpAsync_ExpiredChallenge_ReturnsExpired()
    {
        var service = new OtpService(_repository, _otpGenerator, _otpHasher, _smsProvider, _dateTimeProvider);
        var requestResult = await service.RequestOtpAsync(_testPhone);

        var sentMessage = _smsProvider.GetSentMessages().First().Message;
        var otpCode = sentMessage.Split(':')[1].Trim().Substring(0, 6);

        // Advance clock past 5 minutes
        _dateTimeProvider.UtcNow = _dateTimeProvider.UtcNow.AddMinutes(5).AddSeconds(1);

        var verifyResult = await service.VerifyOtpAsync(requestResult.ChallengeId!.Value, otpCode);

        Assert.False(verifyResult.IsSuccess);
        Assert.Equal(OtpVerificationResult.Expired, verifyResult.Status);
    }

    [Fact]
    public async Task VerifyOtpAsync_AlreadyVerified_PreventsReuse()
    {
        var service = new OtpService(_repository, _otpGenerator, _otpHasher, _smsProvider, _dateTimeProvider);
        var requestResult = await service.RequestOtpAsync(_testPhone);

        var sentMessage = _smsProvider.GetSentMessages().First().Message;
        var otpCode = sentMessage.Split(':')[1].Trim().Substring(0, 6);

        var firstResult = await service.VerifyOtpAsync(requestResult.ChallengeId!.Value, otpCode);
        Assert.True(firstResult.IsSuccess);

        var secondResult = await service.VerifyOtpAsync(requestResult.ChallengeId!.Value, otpCode);
        Assert.False(secondResult.IsSuccess);
        Assert.Equal(OtpVerificationResult.AlreadyCompleted, secondResult.Status);
    }

    [Fact]
    public async Task VerifyOtpAsync_MissingChallenge_ReturnsChallengeNotFound()
    {
        var service = new OtpService(_repository, _otpGenerator, _otpHasher, _smsProvider, _dateTimeProvider);

        var verifyResult = await service.VerifyOtpAsync(OtpChallengeId.New(), "123456");

        Assert.False(verifyResult.IsSuccess);
        Assert.Equal(OtpVerificationResult.ChallengeNotFound, verifyResult.Status);
    }
}
