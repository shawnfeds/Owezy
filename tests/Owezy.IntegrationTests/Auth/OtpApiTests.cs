using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Owezy.Api.Auth;
using Owezy.Application.Auth;
using Owezy.Application.Common;
using Owezy.Domain.Auth;
using Xunit;

namespace Owezy.IntegrationTests.Auth;

public class OtpApiTests : IClassFixture<WebApplicationFactory<Program>>
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
        public DateTimeOffset UtcNow { get; set; } = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);
    }

    private const string TestJwtKey = "api-test-jwt-signing-secret-key-32chars-long-12345";
    private readonly WebApplicationFactory<Program> _factory;

    public OtpApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace Infrastructure repository, time provider, and JWT options with test doubles
                services.AddSingleton<IOtpChallengeRepository, InMemoryOtpChallengeRepository>();
                services.AddSingleton<IDateTimeProvider, TestDateTimeProvider>();
                services.AddSingleton<IOtpHasher>(_ => new HmacSha256OtpHasher("api-test-secret-key-1234567890"));

                var jwtOpts = new JwtOptions
                {
                    SigningKey = TestJwtKey,
                    Issuer = "Owezy.Api.Test",
                    Audience = "Owezy.App.Test",
                    AccessTokenLifetimeMinutes = 15
                };
                services.AddSingleton(jwtOpts);
                services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
            });
        });
    }

    [Fact]
    public async Task RequestOtp_ValidPhone_Returns202AcceptedWithChallengeId()
    {
        var client = _factory.CreateClient();
        var payload = new { phoneNumber = "+919876543210" };

        var response = await client.PostAsJsonAsync("/auth/otp/request", payload);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("otp", responseBody, StringComparison.OrdinalIgnoreCase);

        var result = JsonSerializer.Deserialize<RequestOtpHttpResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.True(Guid.TryParse(result.ChallengeId, out _));
    }

    [Fact]
    public async Task RequestOtp_MissingOrInvalidPhone_Returns400BadRequest()
    {
        var client = _factory.CreateClient();

        // Missing phone
        var response1 = await client.PostAsJsonAsync("/auth/otp/request", new { phoneNumber = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response1.StatusCode);

        // Invalid phone format
        var response2 = await client.PostAsJsonAsync("/auth/otp/request", new { phoneNumber = "invalid" });
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
    }

    [Fact]
    public async Task VerifyOtp_CorrectOtp_Returns200OKWithAccessToken()
    {
        var client = _factory.CreateClient();

        // 1. Request OTP
        var reqResponse = await client.PostAsJsonAsync("/auth/otp/request", new { phoneNumber = "+919876543210" });
        var reqData = await reqResponse.Content.ReadFromJsonAsync<RequestOtpHttpResponse>();
        Assert.NotNull(reqData);

        // Retrieve development SMS captured in DevSmsProvider
        var devSms = _factory.Services.GetRequiredService<ISmsProvider>() as DevelopmentSmsProvider;
        Assert.NotNull(devSms);
        var lastMsg = devSms.GetSentMessages().Last().Message;
        var otpCode = lastMsg.Split(':')[1].Trim().Substring(0, 6);

        // 2. Verify OTP
        var verifyPayload = new { challengeId = reqData.ChallengeId, otp = otpCode };
        var verifyResponse = await client.PostAsJsonAsync("/auth/otp/verify", verifyPayload);

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var verifyData = await verifyResponse.Content.ReadFromJsonAsync<VerifyOtpHttpResponse>();
        Assert.NotNull(verifyData);
        Assert.False(string.IsNullOrWhiteSpace(verifyData.AccessToken));
        Assert.Equal("Bearer", verifyData.TokenType);

        // Validate returned JWT token contents
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(verifyData.AccessToken);
        Assert.Equal("+919876543210", jwt.Subject);
        Assert.Equal("Owezy.Api.Test", jwt.Issuer);
    }

    [Fact]
    public async Task VerifyOtp_IncorrectOtp_Returns401UnauthorizedWithoutAccessToken()
    {
        var client = _factory.CreateClient();
        var reqResponse = await client.PostAsJsonAsync("/auth/otp/request", new { phoneNumber = "+919876543210" });
        var reqData = await reqResponse.Content.ReadFromJsonAsync<RequestOtpHttpResponse>();

        var verifyResponse = await client.PostAsJsonAsync("/auth/otp/verify", new { challengeId = reqData!.ChallengeId, otp = "000000" });

        Assert.Equal(HttpStatusCode.Unauthorized, verifyResponse.StatusCode);

        var responseBody = await verifyResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("accessToken", responseBody, StringComparison.OrdinalIgnoreCase);

        var err = JsonSerializer.Deserialize<ApiError>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal("invalid_otp", err!.Code);
    }

    [Fact]
    public async Task VerifyOtp_UnknownChallengeId_Returns404NotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/otp/verify", new { challengeId = Guid.NewGuid().ToString(), otp = "123456" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var err = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("challenge_not_found", err!.Code);
    }

    [Fact]
    public async Task VerifyOtp_AlreadyVerifiedChallenge_Returns409Conflict()
    {
        var client = _factory.CreateClient();
        var reqResponse = await client.PostAsJsonAsync("/auth/otp/request", new { phoneNumber = "+919876543210" });
        var reqData = await reqResponse.Content.ReadFromJsonAsync<RequestOtpHttpResponse>();

        var devSms = _factory.Services.GetRequiredService<ISmsProvider>() as DevelopmentSmsProvider;
        var otpCode = devSms!.GetSentMessages().Last().Message.Split(':')[1].Trim().Substring(0, 6);

        // First verification: 200 OK
        await client.PostAsJsonAsync("/auth/otp/verify", new { challengeId = reqData!.ChallengeId, otp = otpCode });

        // Second verification: 409 Conflict
        var secondResponse = await client.PostAsJsonAsync("/auth/otp/verify", new { challengeId = reqData.ChallengeId, otp = otpCode });

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        var err = await secondResponse.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("challenge_already_used", err!.Code);
    }

    [Fact]
    public async Task VerifyOtp_MalformedInputs_Returns400BadRequest()
    {
        var client = _factory.CreateClient();

        // Malformed challengeId
        var res1 = await client.PostAsJsonAsync("/auth/otp/verify", new { challengeId = "not-a-guid", otp = "123456" });
        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);

        // Missing OTP
        var res2 = await client.PostAsJsonAsync("/auth/otp/verify", new { challengeId = Guid.NewGuid().ToString(), otp = "" });
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);

        // Malformed OTP length/character
        var res3 = await client.PostAsJsonAsync("/auth/otp/verify", new { challengeId = Guid.NewGuid().ToString(), otp = "abc" });
        Assert.Equal(HttpStatusCode.BadRequest, res3.StatusCode);
    }
}
