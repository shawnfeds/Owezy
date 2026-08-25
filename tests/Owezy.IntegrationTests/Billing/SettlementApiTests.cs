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

public class SettlementApiTests : IClassFixture<WebApplicationFactory<Program>>
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

        public Task AddAsync(Bill bill, CancellationToken ct = default) { Store[bill.Id] = bill; return Task.CompletedTask; }
        public Task UpdateAsync(Bill bill, CancellationToken ct = default) { Store[bill.Id] = bill; return Task.CompletedTask; }
    }

    private class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private const string TestJwtKey = "api-test-jwt-signing-secret-key-32chars-long-12345";
    private readonly WebApplicationFactory<Program> _factory;
    private readonly PhoneNumber _splitterPhone = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _participantPhone = PhoneNumber.Create("+919123456789");

    public SettlementApiTests(WebApplicationFactory<Program> factory)
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

    private async Task<(HttpClient splitterClient, string billId, string splitterPartId, string part2Id, string part2Token)> SetupFinalizedBillAsync()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Settlement Event" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var addPartRes = await client.PostAsJsonAsync($"/bills/{billData!.BillId}/participants", new { phoneNumber = _participantPhone.Value });
        var partData = await addPartRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billData.BillId)));
        var splitterPartId = bill!.Participants.First().Id.Value.ToString();

        await client.PostAsJsonAsync($"/bills/{billData.BillId}/items", new
        {
            description = "Dinner",
            quantity = 1,
            amount = 1000m,
            sharerParticipantIds = new[] { splitterPartId, partData!.ParticipantId }
        });

        await client.PostAsync($"/bills/{billData.BillId}/finalize", null);

        var linkRes = await client.PostAsync($"/bills/{billData.BillId}/participants/{partData.ParticipantId}/access-link", null);
        var linkData = await linkRes.Content.ReadFromJsonAsync<GenerateAccessLinkHttpResponse>();

        return (client, billData.BillId, splitterPartId, partData.ParticipantId, linkData!.Token);
    }

    [Fact]
    public async Task Splitter_GetSettlement_AllUnpaid_Returns200OK()
    {
        var (client, billId, _, _, _) = await SetupFinalizedBillAsync();

        var res = await client.GetAsync($"/bills/{billId}/settlement");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var data = await res.Content.ReadFromJsonAsync<BillSettlementHttpResponse>();
        Assert.NotNull(data);
        Assert.Equal(billId, data.BillId);
        Assert.Equal(1000m, data.BillTotalAmount);
        Assert.Equal(0m, data.TotalPaid);
        Assert.Equal(1000m, data.TotalRemaining);
        Assert.Equal(2, data.ParticipantCount);
        Assert.Equal(0, data.PaidCount);
        Assert.Equal(2, data.UnpaidCount);
    }

    [Fact]
    public async Task Splitter_GetSettlement_SomePaid_ReflectsCorrectAmounts()
    {
        var (client, billId, _, part2Id, part2Token) = await SetupFinalizedBillAsync();
        var anon = _factory.CreateClient();
        await anon.PostAsync($"/participant-access/{part2Token}/payment", null);

        var res = await client.GetAsync($"/bills/{billId}/settlement");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var data = await res.Content.ReadFromJsonAsync<BillSettlementHttpResponse>();
        Assert.NotNull(data);
        Assert.Equal(1, data.PaidCount);
        Assert.Equal(1, data.UnpaidCount);
        Assert.Equal(500m, data.TotalPaid);
        Assert.Equal(500m, data.TotalRemaining);
        Assert.Equal(1000m, data.TotalOwed);

        var part2 = data.Participants.First(p => p.ParticipantId == part2Id);
        Assert.Equal("Paid", part2.PaymentStatus);
        Assert.Equal(500m, part2.AmountPaid);
        Assert.Equal(0m, part2.AmountRemaining);
    }

    [Fact]
    public async Task UnauthenticatedRequest_Returns401()
    {
        var (_, billId, _, _, _) = await SetupFinalizedBillAsync();
        var client = _factory.CreateClient();

        var res = await client.GetAsync($"/bills/{billId}/settlement");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task NonSplitter_GetSettlement_Returns403()
    {
        var (_, billId, _, _, _) = await SetupFinalizedBillAsync();
        var nonSplitterClient = CreateAuthenticatedClient(_participantPhone);

        var res = await nonSplitterClient.GetAsync($"/bills/{billId}/settlement");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task CrossBillAccess_Returns404()
    {
        var (client, _, _, _, _) = await SetupFinalizedBillAsync();

        var res = await client.GetAsync($"/bills/{Guid.NewGuid()}/settlement");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task OpenBill_Settlement_Returns409()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Open" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billData!.BillId)));
        var splitterPartId = bill!.Participants.First().Id.Value.ToString();

        await client.PostAsJsonAsync($"/bills/{billData.BillId}/items", new
        {
            description = "Item",
            quantity = 1,
            amount = 100m,
            sharerParticipantIds = new[] { splitterPartId }
        });

        var res = await client.GetAsync($"/bills/{billData.BillId}/settlement");

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Settlement_IsReadOnly_DoesNotChangePaymentStatus()
    {
        var (client, billId, _, part2Id, _) = await SetupFinalizedBillAsync();

        await client.GetAsync($"/bills/{billId}/settlement");

        // Check payments endpoint — everyone still unpaid
        var paymentsRes = await client.GetAsync($"/bills/{billId}/payments");
        var paymentsData = await paymentsRes.Content.ReadFromJsonAsync<SplitterBillPaymentsHttpResponse>();
        Assert.All(paymentsData!.ParticipantPayments, p => Assert.Equal("Unpaid", p.PaymentStatus));
    }

    [Fact]
    public async Task Settlement_DoesNotExposeParticipantTokensOrHashes()
    {
        var (client, billId, _, _, _) = await SetupFinalizedBillAsync();

        var res = await client.GetAsync($"/bills/{billId}/settlement");
        var body = await res.Content.ReadAsStringAsync();

        // Token hashes are 64-char hex strings; check none appear
        Assert.DoesNotContain("tokenHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Settlement_ExactMoneyConservation()
    {
        var (client, billId, _, _, _) = await SetupFinalizedBillAsync();

        var res = await client.GetAsync($"/bills/{billId}/settlement");
        var data = await res.Content.ReadFromJsonAsync<BillSettlementHttpResponse>();

        Assert.NotNull(data);
        Assert.Equal(data.TotalOwed, data.TotalPaid + data.TotalRemaining);
    }
}
