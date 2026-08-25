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
using Owezy.Infrastructure.Security;
using Xunit;

namespace Owezy.IntegrationTests.Billing;

public class ParticipantAccessApiTests : IClassFixture<WebApplicationFactory<Program>>
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

        public Task AddAsync(Bill bill, CancellationToken ct = default)
        {
            Store[bill.Id] = bill;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Bill bill, CancellationToken ct = default)
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

    public ParticipantAccessApiTests(WebApplicationFactory<Program> factory)
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

    private async Task<(HttpClient splitterClient, string billId, string splitterParticipantId, string secondParticipantId)> CreateFinalizedBillAsync()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Weekend Trip" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var addPartRes = await client.PostAsJsonAsync($"/bills/{billData!.BillId}/participants", new { phoneNumber = _participantPhone.Value });
        var partData = await addPartRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billData.BillId)));
        var splitterPartId = bill!.Participants.First().Id.Value.ToString();

        await client.PostAsJsonAsync($"/bills/{billData.BillId}/items", new
        {
            description = "Hotel Stay",
            quantity = 1,
            amount = 3000m,
            sharerParticipantIds = new[] { splitterPartId, partData!.ParticipantId }
        });

        await client.PostAsync($"/bills/{billData.BillId}/finalize", null);

        return (client, billData.BillId, splitterPartId, partData.ParticipantId);
    }

    [Fact]
    public async Task AuthenticatedSplitter_GeneratesParticipantAccessLink_Returns200OK()
    {
        var (client, billId, _, part2Id) = await CreateFinalizedBillAsync();

        var res = await client.PostAsync($"/bills/{billId}/participants/{part2Id}/access-link", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var data = await res.Content.ReadFromJsonAsync<GenerateAccessLinkHttpResponse>();
        Assert.NotNull(data);
        Assert.False(string.IsNullOrWhiteSpace(data.Token));
        Assert.Equal(billId, data.BillId);
        Assert.Equal(part2Id, data.ParticipantId);
    }

    [Fact]
    public async Task UnauthenticatedSplitterRequest_Returns401Unauthorized()
    {
        var unauthClient = _factory.CreateClient();
        var res = await unauthClient.PostAsync($"/bills/{Guid.NewGuid()}/participants/{Guid.NewGuid()}/access-link", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task NonSplitterRequest_Returns403Forbidden()
    {
        var (_, billId, _, part2Id) = await CreateFinalizedBillAsync();
        var nonSplitterClient = CreateAuthenticatedClient(_participantPhone);

        var res = await nonSplitterClient.PostAsync($"/bills/{billId}/participants/{part2Id}/access-link", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task GenerateLink_ForOpenBill_Returns409Conflict()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Open Bill" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billData!.BillId)));
        var splitterPartId = bill!.Participants.First().Id.Value.ToString();

        var res = await client.PostAsync($"/bills/{billData.BillId}/participants/{splitterPartId}/access-link", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task ValidParticipantToken_RetrievesParticipantView()
    {
        var (client, billId, _, part2Id) = await CreateFinalizedBillAsync();
        var linkRes = await client.PostAsync($"/bills/{billId}/participants/{part2Id}/access-link", null);
        var linkData = await linkRes.Content.ReadFromJsonAsync<GenerateAccessLinkHttpResponse>();

        var anonymousClient = _factory.CreateClient();
        var res = await anonymousClient.GetAsync($"/participant-access/{linkData!.Token}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var view = await res.Content.ReadFromJsonAsync<ParticipantBillViewHttpResponse>();
        Assert.NotNull(view);
        Assert.Equal("Weekend Trip", view.BillTitle);
        Assert.Equal(3000m, view.BillTotalAmount);
        Assert.Equal(part2Id, view.ParticipantId);
        Assert.Equal(_participantPhone.Value, view.ParticipantPhoneNumber);
        Assert.Equal(1500m, view.TotalAmountOwed);
        Assert.Single(view.Items);
        Assert.Equal("Hotel Stay", view.Items[0].Description);
        Assert.Equal(1500m, view.Items[0].MyShareAmount);
    }

    [Fact]
    public async Task InvalidToken_Returns404NotFound()
    {
        var anonymousClient = _factory.CreateClient();
        var res = await anonymousClient.GetAsync("/participant-access/invalid-opaque-token-string-999");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task ParticipantToken_CannotBeUsedAsJwtBearerToken()
    {
        var (client, billId, _, part2Id) = await CreateFinalizedBillAsync();
        var linkRes = await client.PostAsync($"/bills/{billId}/participants/{part2Id}/access-link", null);
        var linkData = await linkRes.Content.ReadFromJsonAsync<GenerateAccessLinkHttpResponse>();

        var tokenClient = _factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", linkData!.Token);

        var mutateRes = await tokenClient.PostAsJsonAsync($"/bills/{billId}/items", new
        {
            description = "Unauthorized Item",
            quantity = 1,
            amount = 100m,
            sharerParticipantIds = new[] { part2Id }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, mutateRes.StatusCode);
    }
}
