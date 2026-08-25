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

public class BillFinalizeApiTests : IClassFixture<WebApplicationFactory<Program>>
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

    public BillFinalizeApiTests(WebApplicationFactory<Program> factory)
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
        var token = tokenService.GenerateAccessToken(phone);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    private async Task<(HttpClient client, string billId, string splitterParticipantId)> CreateBillWithItemAsync()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Test Bill" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billData!.BillId)));
        var splitterPartId = bill!.Participants.First().Id.Value.ToString();

        await client.PostAsJsonAsync($"/bills/{billData.BillId}/items", new
        {
            description = "Pizza",
            quantity = 1,
            amount = 500m,
            sharerParticipantIds = new[] { splitterPartId }
        });

        return (client, billData.BillId, splitterPartId);
    }

    [Fact]
    public async Task FinalizeBill_AuthenticatedSplitter_Returns200OK()
    {
        var (client, billId, _) = await CreateBillWithItemAsync();

        var res = await client.PostAsync($"/bills/{billId}/finalize", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var data = await res.Content.ReadFromJsonAsync<FinalizeBillHttpResponse>();
        Assert.NotNull(data);
        Assert.Equal("Finalized", data.Status);
        Assert.NotNull(data.FinalizedAt);
    }

    [Fact]
    public async Task FinalizeBill_UnauthenticatedCaller_Returns401Unauthorized()
    {
        var unauthClient = _factory.CreateClient();
        var res = await unauthClient.PostAsync($"/bills/{Guid.NewGuid()}/finalize", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task FinalizeBill_NonSplitterParticipant_Returns403Forbidden()
    {
        var splitterClient = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await splitterClient.PostAsJsonAsync("/bills", new { title = "Dinner" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();
        await splitterClient.PostAsJsonAsync($"/bills/{billData!.BillId}/participants",
            new { phoneNumber = _participantPhone.Value });

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billData.BillId)));
        var splitterPartId = bill!.Participants.First().Id.Value.ToString();
        await splitterClient.PostAsJsonAsync($"/bills/{billData.BillId}/items", new
        {
            description = "Food",
            quantity = 1,
            amount = 200m,
            sharerParticipantIds = new[] { splitterPartId }
        });

        var nonSplitterClient = CreateAuthenticatedClient(_participantPhone);
        var res = await nonSplitterClient.PostAsync($"/bills/{billData.BillId}/finalize", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task FinalizeBill_MissingBill_Returns404NotFound()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var res = await client.PostAsync($"/bills/{Guid.NewGuid()}/finalize", null);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task FinalizeBill_AlreadyFinalized_Returns409Conflict()
    {
        var (client, billId, _) = await CreateBillWithItemAsync();
        await client.PostAsync($"/bills/{billId}/finalize", null);

        var res2 = await client.PostAsync($"/bills/{billId}/finalize", null);
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }

    [Fact]
    public async Task FinalizeBill_EmptyBillNoItems_Returns409Conflict()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Empty Bill" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var res = await client.PostAsync($"/bills/{billData!.BillId}/finalize", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task AddParticipant_AfterFinalization_Returns409Conflict()
    {
        var (client, billId, _) = await CreateBillWithItemAsync();
        await client.PostAsync($"/bills/{billId}/finalize", null);

        var addRes = await client.PostAsJsonAsync($"/bills/{billId}/participants",
            new { phoneNumber = _participantPhone.Value });
        Assert.Equal(HttpStatusCode.Conflict, addRes.StatusCode);
    }

    [Fact]
    public async Task AddItem_AfterFinalization_Returns409Conflict()
    {
        var (client, billId, splitterPartId) = await CreateBillWithItemAsync();
        await client.PostAsync($"/bills/{billId}/finalize", null);

        var addRes = await client.PostAsJsonAsync($"/bills/{billId}/items", new
        {
            description = "Drink",
            quantity = 1,
            amount = 100m,
            sharerParticipantIds = new[] { splitterPartId }
        });
        Assert.Equal(HttpStatusCode.Conflict, addRes.StatusCode);
    }
}
