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

public class PaymentTrackingApiTests : IClassFixture<WebApplicationFactory<Program>>
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

    public PaymentTrackingApiTests(WebApplicationFactory<Program> factory)
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

    private async Task<(HttpClient splitterClient, string billId, string splitterParticipantId, string secondParticipantId, string secondParticipantToken)> CreateFinalizedBillWithAccessLinkAsync()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Dinner Event" });
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
            amount = 1200m,
            sharerParticipantIds = new[] { splitterPartId, partData!.ParticipantId }
        });

        await client.PostAsync($"/bills/{billData.BillId}/finalize", null);

        var linkRes = await client.PostAsync($"/bills/{billData.BillId}/participants/{partData.ParticipantId}/access-link", null);
        var linkData = await linkRes.Content.ReadFromJsonAsync<GenerateAccessLinkHttpResponse>();

        return (client, billData.BillId, splitterPartId, partData.ParticipantId, linkData!.Token);
    }

    [Fact]
    public async Task Participant_MarksSelfPaid_Returns200OK()
    {
        var (_, _, _, part2Id, token) = await CreateFinalizedBillWithAccessLinkAsync();
        var client = _factory.CreateClient();

        var res = await client.PostAsync($"/participant-access/{token}/payment", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var data = await res.Content.ReadFromJsonAsync<MarkParticipantPaidHttpResponse>();
        Assert.NotNull(data);
        Assert.Equal(part2Id, data.ParticipantId);
        Assert.Equal("Paid", data.PaymentStatus);
        Assert.NotNull(data.PaidAt);
    }

    [Fact]
    public async Task Splitter_GetsGroupPaymentStatus_Returns200OK()
    {
        var (splitterClient, billId, splitterPartId, part2Id, token) = await CreateFinalizedBillWithAccessLinkAsync();
        var anonymousClient = _factory.CreateClient();

        await anonymousClient.PostAsync($"/participant-access/{token}/payment", null);

        var res = await splitterClient.GetAsync($"/bills/{billId}/payments");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var data = await res.Content.ReadFromJsonAsync<SplitterBillPaymentsHttpResponse>();
        Assert.NotNull(data);
        Assert.Equal(billId, data.BillId);
        Assert.Equal(1200m, data.BillTotalAmount);
        Assert.Equal(2, data.ParticipantPayments.Count);

        var part2Status = data.ParticipantPayments.First(p => p.ParticipantId == part2Id);
        Assert.Equal("Paid", part2Status.PaymentStatus);
        Assert.Equal(600m, part2Status.AmountOwed);

        var splitterStatus = data.ParticipantPayments.First(p => p.ParticipantId == splitterPartId);
        Assert.Equal("Unpaid", splitterStatus.PaymentStatus);
        Assert.Equal(600m, splitterStatus.AmountOwed);
    }

    [Fact]
    public async Task NonSplitter_CannotGetGroupPaymentStatus_Returns403Forbidden()
    {
        var (_, billId, _, _, _) = await CreateFinalizedBillWithAccessLinkAsync();
        var nonSplitterClient = CreateAuthenticatedClient(_participantPhone);

        var res = await nonSplitterClient.GetAsync($"/bills/{billId}/payments");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedSplitterPaymentsEndpoint_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync($"/bills/{Guid.NewGuid()}/payments");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ParticipantView_IncludesPaymentStatus()
    {
        var (_, _, _, _, token) = await CreateFinalizedBillWithAccessLinkAsync();
        var client = _factory.CreateClient();

        var view1Res = await client.GetAsync($"/participant-access/{token}");
        var view1Data = await view1Res.Content.ReadFromJsonAsync<ParticipantBillViewHttpResponse>();
        Assert.Equal("Unpaid", view1Data!.PaymentStatus);

        await client.PostAsync($"/participant-access/{token}/payment", null);

        var view2Res = await client.GetAsync($"/participant-access/{token}");
        var view2Data = await view2Res.Content.ReadFromJsonAsync<ParticipantBillViewHttpResponse>();
        Assert.Equal("Paid", view2Data!.PaymentStatus);
        Assert.NotNull(view2Data.PaidAt);
    }
}
