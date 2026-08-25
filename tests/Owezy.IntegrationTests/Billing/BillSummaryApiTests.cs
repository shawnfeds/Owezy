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

public class BillSummaryApiTests : IClassFixture<WebApplicationFactory<Program>>
{
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

    private class TestDateTimeProvider : IDateTimeProvider { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

    private const string TestJwtKey = "api-test-jwt-signing-secret-key-32chars-long-12345";
    private readonly WebApplicationFactory<Program> _factory;
    private readonly PhoneNumber _splitterPhone = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _otherPhone = PhoneNumber.Create("+919123456789");

    public BillSummaryApiTests(WebApplicationFactory<Program> factory)
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
                var jwtOpts = new JwtOptions { SigningKey = TestJwtKey, Issuer = "Owezy.Api", Audience = "Owezy.App", AccessTokenLifetimeMinutes = 15 };
                services.AddSingleton(jwtOpts);
                services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
            });
        });
    }

    private HttpClient CreateAuthenticatedClient(PhoneNumber phone)
    {
        var token = _factory.Services.GetRequiredService<IAccessTokenService>().GenerateAccessToken(phone);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    [Fact]
    public async Task Splitter_GetBillSummary_Returns200WithAllData()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);

        // Create bill
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Summary Test" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();
        var billId = billData!.BillId;

        // Add participant
        var addPartRes = await client.PostAsJsonAsync($"/bills/{billId}/participants", new { phoneNumber = _otherPhone.Value });
        var partData = await addPartRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billId)));
        var splitterPartId = bill!.Participants.First(p => p.PhoneNumber == _splitterPhone).Id.Value.ToString();

        // Add item shared by both
        await client.PostAsJsonAsync($"/bills/{billId}/items", new
        {
            description = "Dinner",
            quantity = 1,
            amount = 300m,
            sharerParticipantIds = new[] { splitterPartId, partData!.ParticipantId }
        });

        // GET summary
        var summaryRes = await client.GetAsync($"/bills/{billId}");
        Assert.Equal(HttpStatusCode.OK, summaryRes.StatusCode);

        var summary = await summaryRes.Content.ReadFromJsonAsync<SplitterBillSummaryHttpResponse>();
        Assert.NotNull(summary);
        Assert.Equal(billId, summary.BillId);
        Assert.Equal("Summary Test", summary.Title);
        Assert.Equal("Active", summary.Status);
        Assert.Equal(300m, summary.TotalAmount);
        Assert.Equal(2, summary.Participants.Count);
        Assert.Single(summary.Items);

        var item = summary.Items.Single();
        Assert.Equal("Dinner", item.Description);
        Assert.Equal(300m, item.Amount);
        Assert.Equal(2, item.CalculatedShares.Count);
        // Each participant gets 150
        Assert.All(item.CalculatedShares, s => Assert.Equal(150m, s.Amount));
    }

    [Fact]
    public async Task NonSplitter_GetBillSummary_Returns403()
    {
        var splitterClient = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await splitterClient.PostAsJsonAsync("/bills", new { title = "Summary Test" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var otherClient = CreateAuthenticatedClient(_otherPhone);
        var res = await otherClient.GetAsync($"/bills/{billData!.BillId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task SplitterSummary_FinalizedBill_Returns200WithFinalizedStatus()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Finalized Summary" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();
        var billId = billData!.BillId;

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billId)));
        var splitterPartId = bill!.Participants.First().Id.Value.ToString();

        await client.PostAsJsonAsync($"/bills/{billId}/items", new
        {
            description = "Item",
            quantity = 1,
            amount = 100m,
            sharerParticipantIds = new[] { splitterPartId }
        });

        await client.PostAsync($"/bills/{billId}/finalize", null);

        var summaryRes = await client.GetAsync($"/bills/{billId}");
        Assert.Equal(HttpStatusCode.OK, summaryRes.StatusCode);
        var summary = await summaryRes.Content.ReadFromJsonAsync<SplitterBillSummaryHttpResponse>();
        Assert.Equal("Finalized", summary!.Status);
        Assert.NotNull(summary.FinalizedAt);
    }

    [Fact]
    public async Task SplitterSummary_MissingBill_Returns404()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var res = await client.GetAsync($"/bills/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task ParticipantSummary_ValidToken_Returns200WithScopedData()
    {
        var splitterClient = CreateAuthenticatedClient(_splitterPhone);

        var createRes = await splitterClient.PostAsJsonAsync("/bills", new { title = "Participant Summary Test" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();
        var billId = billData!.BillId;

        var addPartRes = await splitterClient.PostAsJsonAsync($"/bills/{billId}/participants", new { phoneNumber = _otherPhone.Value });
        var partData = await addPartRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billId)));
        var splitterPartId = bill!.Participants.First(p => p.PhoneNumber == _splitterPhone).Id.Value.ToString();

        // One item shared only by splitter, one shared by both
        await splitterClient.PostAsJsonAsync($"/bills/{billId}/items", new
        {
            description = "Splitter Only",
            quantity = 1,
            amount = 200m,
            sharerParticipantIds = new[] { splitterPartId }
        });

        await splitterClient.PostAsJsonAsync($"/bills/{billId}/items", new
        {
            description = "Shared",
            quantity = 1,
            amount = 300m,
            sharerParticipantIds = new[] { splitterPartId, partData!.ParticipantId }
        });

        await splitterClient.PostAsync($"/bills/{billId}/finalize", null);

        var linkRes = await splitterClient.PostAsync($"/bills/{billId}/participants/{partData.ParticipantId}/access-link", null);
        var linkData = await linkRes.Content.ReadFromJsonAsync<GenerateAccessLinkHttpResponse>();

        // Participant accesses their summary
        var anonClient = _factory.CreateClient();
        var summaryRes = await anonClient.GetAsync($"/participant-access/{linkData!.Token}/summary");
        Assert.Equal(HttpStatusCode.OK, summaryRes.StatusCode);

        var summary = await summaryRes.Content.ReadFromJsonAsync<ParticipantBillViewHttpResponse>();
        Assert.NotNull(summary);
        Assert.Equal(partData.ParticipantId, summary.ParticipantId);
        // Participant only sees the shared item (300 / 2 = 150 owed)
        Assert.Single(summary.Items);
        Assert.Equal(150m, summary.TotalAmountOwed);
        // Does NOT see splitter-only item
        Assert.DoesNotContain(summary.Items, i => i.Description == "Splitter Only");
    }

    [Fact]
    public async Task ParticipantSummary_InvalidToken_Returns404()
    {
        var anonClient = _factory.CreateClient();
        var res = await anonClient.GetAsync("/participant-access/invalid-token-xyz/summary");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
