using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Owezy.Api.Auth;
using Owezy.Api.Billing;
using Owezy.Application.Auth;
using Owezy.Application.Billing;
using Owezy.Application.Common;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Xunit;

namespace Owezy.IntegrationTests.Billing;

public class BillApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private class InMemoryBillRepository : IBillRepository
    {
        public Dictionary<BillId, Bill> Store { get; } = new();

        public Task<Bill?> GetByIdAsync(BillId id, CancellationToken ct = default)
        {
            Store.TryGetValue(id, out var b);
            return Task.FromResult(b);
        }

        public Task<Bill?> GetByAccessLinkHashAsync(string tokenHash, CancellationToken ct = default)
        {
            var bill = Store.Values.FirstOrDefault(b => b.AccessLinks.Any(l => l.TokenHash == tokenHash && !l.IsRevoked));
            return Task.FromResult(bill);
        }

        public Task AddAsync(Bill bill, CancellationToken cancellationToken = default)
        {
            Store[bill.Id] = bill;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Bill bill, CancellationToken cancellationToken = default)
        {
            Store[bill.Id] = bill;
            return Task.CompletedTask;
        }
    }

    private class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private const string TestJwtKey = "api-test-jwt-signing-secret-key-32chars-long-12345";
    private readonly WebApplicationFactory<Program> _factory;
    private readonly PhoneNumber _splitterPhone = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _participantPhone = PhoneNumber.Create("+919123456789");

    public BillApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Jwt:SigningKey", TestJwtKey);
            builder.UseSetting("Jwt:Issuer", "Owezy.Api");
            builder.UseSetting("Jwt:Audience", "Owezy.App");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IBillRepository, InMemoryBillRepository>();
                services.AddSingleton<IDateTimeProvider, TestDateTimeProvider>();

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
        var tokenResult = tokenService.GenerateAccessToken(phone);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);
        return client;
    }

    [Fact]
    public async Task CreateBill_UnauthenticatedRequest_Returns401Unauthorized()
    {
        var client = _factory.CreateClient(); // No auth header

        var response = await client.PostAsJsonAsync("/bills", new { title = "Weekend Getaway" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateBill_AuthenticatedCaller_CreatesBillWithSplitterFromToken()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);

        var response = await client.PostAsJsonAsync("/bills", new { title = "Weekend Getaway" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var billData = await response.Content.ReadFromJsonAsync<CreateBillHttpResponse>();
        Assert.NotNull(billData);
        Assert.Equal("Weekend Getaway", billData.Title);
        Assert.Equal(_splitterPhone.Value, billData.SplitterPhoneNumber); // Comes from token, not client body!
        Assert.Equal(1, billData.ParticipantCount); // Splitter is initial participant
    }

    [Fact]
    public async Task AddParticipant_AuthenticatedMember_AddsParticipantAndReturns200OK()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);

        // 1. Create Bill
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Dinner Party" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        // 2. Add Participant
        var addRes = await client.PostAsJsonAsync($"/bills/{billData!.BillId}/participants", new { phoneNumber = _participantPhone.Value });

        Assert.Equal(HttpStatusCode.OK, addRes.StatusCode);
        var partData = await addRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();
        Assert.NotNull(partData);
        Assert.Equal(_participantPhone.Value, partData.PhoneNumber);
        Assert.Equal(billData.BillId, partData.BillId);
    }

    [Fact]
    public async Task AddParticipant_DuplicateParticipant_Returns409Conflict()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);

        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Dinner Party" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        // Adding the splitter again (splitter is already initial participant)
        var addRes = await client.PostAsJsonAsync($"/bills/{billData!.BillId}/participants", new { phoneNumber = _splitterPhone.Value });

        Assert.Equal(HttpStatusCode.Conflict, addRes.StatusCode);
        var err = await addRes.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("duplicate_participant", err!.Code);
    }
}
