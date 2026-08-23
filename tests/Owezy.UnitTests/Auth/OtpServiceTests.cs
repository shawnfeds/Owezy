using Owezy.Application.Auth;
using Owezy.Application.Common;
using Owezy.Domain.Auth;
using Xunit;

namespace Owezy.UnitTests.Auth;

public class OtpServiceTests
{
    private class InMemoryOtpChallengeRepository : IOtpChallengeRepository
    {
        private readonly Dictionary<OtpChallengeId, OtpChallenge> _store = new();

        public Task<OtpChallenge?> GetByIdAsync(OtpChallengeId id, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(id, out var challenge);
            return Task.FromResult(challenge);
        }

        public Task AddAsync(OtpChallenge challenge, CancellationToken cancellationToken = default)
        {
            _store[challenge.Id] = challenge;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(OtpChallenge challenge, CancellationToken cancellationToken = default)
        {
            _store[challenge.Id] = challenge;
            return Task.CompletedTask;
        }
    }

    private class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly InMemoryOtpChallengeRepository _repository = new();
    private readonly SecureOtpGenerator _otpGenerator = new();
    private readonly HmacSha256OtpHasher _otpHasher = new("test-service-secret-key-1234567890");
    private readonly DevelopmentSmsProvider _smsProvider = new();
    private readonly TestDateTimeProvider _dateTimeProvider = new();
    private readonly PhoneNumber _testPhone = PhoneNumber.Create("+919876543210");

    [Fact]
    public async Task CreateChallengeAsync_GeneratesChallengeAndSendsSmsViaDevProvider()
    {
        var service = new OtpService(_repository, _otpGenerator, _otpHasher, _smsProvider, _dateTimeProvider);

        var result = await service.CreateChallengeAsync(_testPhone);

        Assert.NotEqual(Guid.Empty, result.ChallengeId.Value);
        Assert.Equal(_dateTimeProvider.UtcNow.AddMinutes(5), result.ExpiresAt);
        Assert.Equal(5, result.RemainingAttempts);

        var sentMessages = _smsProvider.GetSentMessages();
        Assert.Single(sentMessages);
        var sent = sentMessages.First();
        Assert.Equal(_testPhone, sent.Recipient);
        Assert.Contains("Your Owezy verification code is:", sent.Message);
    }

    [Fact]
    public async Task VerifyChallengeAsync_ValidOtp_ReturnsSuccess()
    {
        var service = new OtpService(_repository, _otpGenerator, _otpHasher, _smsProvider, _dateTimeProvider);
        var createResult = await service.CreateChallengeAsync(_testPhone);

        var sentMessage = _smsProvider.GetSentMessages().First().Message;
        // Extract 6 digit OTP from message: "Your Owezy verification code is: 123456. Valid for 5 minutes."
        var otpCode = sentMessage.Split(':')[1].Trim().Substring(0, 6);

        var verifyResult = await service.VerifyChallengeAsync(createResult.ChallengeId, otpCode);

        Assert.Equal(OtpVerificationResult.Success, verifyResult);
    }

    [Fact]
    public async Task VerifyChallengeAsync_InvalidOtp_ReturnsInvalidOtp()
    {
        var service = new OtpService(_repository, _otpGenerator, _otpHasher, _smsProvider, _dateTimeProvider);
        var createResult = await service.CreateChallengeAsync(_testPhone);

        var verifyResult = await service.VerifyChallengeAsync(createResult.ChallengeId, "999999");

        Assert.Equal(OtpVerificationResult.InvalidOtp, verifyResult);
    }

    [Fact]
    public async Task VerifyChallengeAsync_ExpiredTime_ReturnsExpired()
    {
        var service = new OtpService(_repository, _otpGenerator, _otpHasher, _smsProvider, _dateTimeProvider);
        var createResult = await service.CreateChallengeAsync(_testPhone);

        var sentMessage = _smsProvider.GetSentMessages().First().Message;
        var otpCode = sentMessage.Split(':')[1].Trim().Substring(0, 6);

        // Advance time past 5 minutes
        _dateTimeProvider.UtcNow = _dateTimeProvider.UtcNow.AddMinutes(5).AddSeconds(1);

        var verifyResult = await service.VerifyChallengeAsync(createResult.ChallengeId, otpCode);

        Assert.Equal(OtpVerificationResult.Expired, verifyResult);
    }

    [Fact]
    public async Task VerifyChallengeAsync_NonExistentChallenge_ReturnsChallengeNotFound()
    {
        var service = new OtpService(_repository, _otpGenerator, _otpHasher, _smsProvider, _dateTimeProvider);

        var verifyResult = await service.VerifyChallengeAsync(OtpChallengeId.New(), "123456");

        Assert.Equal(OtpVerificationResult.ChallengeNotFound, verifyResult);
    }
}
