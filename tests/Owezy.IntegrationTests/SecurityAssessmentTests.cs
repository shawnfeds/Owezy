using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Owezy.Api.Auth;
using Owezy.Api.Billing;
using Owezy.Api.Receipts;
using Owezy.Application.Auth;
using Owezy.Application.Billing;
using Owezy.Application.Common;
using Owezy.Application.Receipts;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Owezy.Domain.Receipts;
using Owezy.Infrastructure.Security;
using Xunit;

namespace Owezy.IntegrationTests;

public class SecurityAssessmentTests : IClassFixture<WebApplicationFactory<Program>>
{
    private class InMemoryOtpChallengeRepository : IOtpChallengeRepository
    {
        public Dictionary<OtpChallengeId, OtpChallenge> Store { get; } = new();
        public Task<OtpChallenge?> GetByIdAsync(OtpChallengeId id, CancellationToken ct = default) { Store.TryGetValue(id, out var c); return Task.FromResult(c); }
        public Task AddAsync(OtpChallenge challenge, CancellationToken ct = default) { Store[challenge.Id] = challenge; return Task.CompletedTask; }
        public Task UpdateAsync(OtpChallenge challenge, CancellationToken ct = default) { Store[challenge.Id] = challenge; return Task.CompletedTask; }
    }

    private class InMemoryBillRepository : IBillRepository
    {
        public Dictionary<BillId, Bill> Store { get; } = new();
        public Task<Bill?> GetByIdAsync(BillId id, CancellationToken ct = default) { Store.TryGetValue(id, out var b); return Task.FromResult(b); }
        public Task<Bill?> GetByAccessLinkHashAsync(string tokenHash, CancellationToken ct = default)
        {
            var bill = Store.Values.FirstOrDefault(b => b.AccessLinks.Any(l => l.TokenHash == tokenHash && !l.IsRevoked));
            return Task.FromResult(bill);
        }
        public Task AddAsync(Bill bill, CancellationToken ct = default) { Store[bill.Id] = bill; return Task.CompletedTask; }
        public Task UpdateAsync(Bill bill, CancellationToken ct = default) { Store[bill.Id] = bill; return Task.CompletedTask; }
    }

    private class InMemoryReceiptRepository : IReceiptRepository
    {
        public Dictionary<ReceiptId, Receipt> Store { get; } = new();
        public Task AddAsync(Receipt receipt, CancellationToken ct = default) { Store[receipt.Id] = receipt; return Task.CompletedTask; }
        public Task<Receipt?> GetByIdAsync(ReceiptId id, CancellationToken ct = default) { Store.TryGetValue(id, out var r); return Task.FromResult(r); }
        public Task UpdateAsync(Receipt receipt, CancellationToken ct = default) { Store[receipt.Id] = receipt; return Task.CompletedTask; }
    }

    private class InMemoryReceiptStorage : IReceiptStorage
    {
        public Task<string> StoreAsync(Stream imageStream, string fileExtension, CancellationToken ct = default)
        {
            return Task.FromResult($"{Guid.NewGuid():N}.{fileExtension}");
        }
    }

    private class TestOcrService : IOcrService
    {
        public Task<OcrReceiptDraft> ProcessAsync(Stream imageStream, CancellationToken ct = default)
        {
            return Task.FromResult(new OcrReceiptDraft
            {
                MerchantName = "Secure Cafe",
                Total = 100m,
                LineItems = new[] { new OcrLineItem { Description = "Item 1", Quantity = 1m, UnitPrice = 100m, LineTotal = 100m } }
            });
        }
    }

    private class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private const string TestJwtKey = "api-test-jwt-signing-secret-key-32chars-long-12345";
    private readonly WebApplicationFactory<Program> _factory;
    private readonly PhoneNumber _splitterPhoneA = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _splitterPhoneB = PhoneNumber.Create("+919123456789");

    public SecurityAssessmentTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Jwt:SigningKey", TestJwtKey);
            builder.UseSetting("Jwt:Issuer", "Owezy.Api");
            builder.UseSetting("Jwt:Audience", "Owezy.App");
            builder.UseSetting("OtpHasher:SecretKey", "test-otp-hasher-secret-key-32chars-long-12345");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IOtpChallengeRepository, InMemoryOtpChallengeRepository>();
                services.AddSingleton<IBillRepository, InMemoryBillRepository>();
                services.AddSingleton<IReceiptRepository, InMemoryReceiptRepository>();
                services.AddSingleton<IReceiptStorage, InMemoryReceiptStorage>();
                services.AddSingleton<IOcrService, TestOcrService>();
                services.AddSingleton<IDateTimeProvider, TestDateTimeProvider>();
                services.AddSingleton<IParticipantTokenGenerator, CryptoParticipantTokenGenerator>();

                var jwtOpts = new JwtOptions
                {
                    SigningKey = TestJwtKey,
                    Issuer = "Owezy.Api",
                    Audience = "Owezy.App",
                    AccessTokenLifetimeMinutes = 15
                };
                services.AddSingleton(jwtOpts);
                services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
            });
        });
    }

    private HttpClient CreateAuthenticatedClient(PhoneNumber phone)
    {
        var tokenService = _factory.Services.GetRequiredService<IAccessTokenService>();
        var token = tokenService.GenerateAccessToken(phone);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    [Fact]
    public async Task Authentication_MalformedJwtToken_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.token.payload");

        var response = await client.GetAsync($"/bills/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authorization_SplitterBCannotAccessSplitterABill_Returns403Forbidden()
    {
        var clientA = CreateAuthenticatedClient(_splitterPhoneA);
        var createRes = await clientA.PostAsJsonAsync("/bills", new { title = "Bill A" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var clientB = CreateAuthenticatedClient(_splitterPhoneB);
        var res = await clientB.GetAsync($"/bills/{billData!.BillId}");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Authorization_SplitterBCannotFinalizeSplitterABill_Returns403Forbidden()
    {
        var clientA = CreateAuthenticatedClient(_splitterPhoneA);
        var createRes = await clientA.PostAsJsonAsync("/bills", new { title = "Bill A" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var clientB = CreateAuthenticatedClient(_splitterPhoneB);
        var res = await clientB.PostAsync($"/bills/{billData!.BillId}/finalize", null);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task ParticipantIsolation_ParticipantTokenCannotAccessOtherBillData_Returns404()
    {
        var clientA = CreateAuthenticatedClient(_splitterPhoneA);
        var createRes = await clientA.PostAsJsonAsync("/bills", new { title = "Bill A" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var addPartRes = await clientA.PostAsJsonAsync($"/bills/{billData!.BillId}/participants", new { phoneNumber = _splitterPhoneB.Value });
        var partData = await addPartRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();

        await clientA.PostAsJsonAsync($"/bills/{billData.BillId}/items", new
        {
            description = "Item",
            quantity = 1,
            amount = 100m,
            sharerParticipantIds = new[] { partData!.ParticipantId }
        });

        await clientA.PostAsync($"/bills/{billData.BillId}/finalize", null);

        var linkRes = await clientA.PostAsync($"/bills/{billData.BillId}/participants/{partData.ParticipantId}/access-link", null);
        var linkData = await linkRes.Content.ReadFromJsonAsync<GenerateAccessLinkHttpResponse>();

        var anonClient = _factory.CreateClient();
        // Tampered token
        var tamperedToken = linkData!.Token.Substring(0, linkData.Token.Length - 4) + "0000";
        var viewRes = await anonClient.GetAsync($"/participant-access/{tamperedToken}");

        Assert.Equal(HttpStatusCode.NotFound, viewRes.StatusCode);
    }

    [Fact]
    public async Task ErrorResponse_DoesNotExposeStackTracesOrSecrets()
    {
        var client = CreateAuthenticatedClient(_splitterPhoneA);
        // Non-existent bill GUID
        var res = await client.GetAsync($"/bills/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var content = await res.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Exception", content);
        Assert.DoesNotContain("StackTrace", content);
        Assert.DoesNotContain(TestJwtKey, content);
    }
}
