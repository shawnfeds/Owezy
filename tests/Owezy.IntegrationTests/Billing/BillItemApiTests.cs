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

public class BillItemApiTests : IClassFixture<WebApplicationFactory<Program>>
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

    public BillItemApiTests(WebApplicationFactory<Program> factory)
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
    public async Task AddBillItem_AuthenticatedSplitter_CreatesItemAndReturns201Created()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);

        // 1. Create bill
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Movie & Drinks" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();
        Assert.NotNull(billData);

        // Retrieve initial participant ID (splitter) from repository
        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billData.BillId)));
        var splitterParticipantId = bill!.Participants.First().Id.Value.ToString();

        // 2. Add item
        var itemPayload = new
        {
            description = "Popcorn Combo",
            quantity = 2,
            amount = 550.00m,
            sharerParticipantIds = new[] { splitterParticipantId }
        };

        var itemRes = await client.PostAsJsonAsync($"/bills/{billData.BillId}/items", itemPayload);

        Assert.Equal(HttpStatusCode.Created, itemRes.StatusCode);
        var itemData = await itemRes.Content.ReadFromJsonAsync<AddBillItemHttpResponse>();
        Assert.NotNull(itemData);
        Assert.Equal("Popcorn Combo", itemData.Description);
        Assert.Equal(2, itemData.Quantity);
        Assert.Equal(550.00m, itemData.Amount);
        Assert.Single(itemData.SharerParticipantIds);
        Assert.Contains(splitterParticipantId, itemData.SharerParticipantIds);
    }

    [Fact]
    public async Task AddBillItem_UnauthenticatedCaller_Returns401Unauthorized()
    {
        var unauthClient = _factory.CreateClient(); // No token

        var response = await unauthClient.PostAsJsonAsync($"/bills/{Guid.NewGuid()}/items", new
        {
            description = "Pizza",
            quantity = 1,
            amount = 500m,
            sharerParticipantIds = new[] { Guid.NewGuid().ToString() }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddBillItem_NonSplitterParticipant_Returns403Forbidden()
    {
        var splitterClient = CreateAuthenticatedClient(_splitterPhone);

        // Splitter creates bill and adds participant
        var createRes = await splitterClient.PostAsJsonAsync("/bills", new { title = "Lunch" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        await splitterClient.PostAsJsonAsync($"/bills/{billData!.BillId}/participants", new { phoneNumber = _participantPhone.Value });

        // Non-splitter participant tries to add item
        var nonSplitterClient = CreateAuthenticatedClient(_participantPhone);
        var res = await nonSplitterClient.PostAsJsonAsync($"/bills/{billData.BillId}/items", new
        {
            description = "Juice",
            quantity = 1,
            amount = 100m,
            sharerParticipantIds = new[] { Guid.NewGuid().ToString() }
        });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task AddBillItem_InvalidQuantityOrAmount_Returns400BadRequest()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Dinner" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        // Zero quantity
        var res1 = await client.PostAsJsonAsync($"/bills/{billData!.BillId}/items", new
        {
            description = "Bread",
            quantity = 0,
            amount = 100m,
            sharerParticipantIds = new[] { Guid.NewGuid().ToString() }
        });
        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);

        // Zero amount
        var res2 = await client.PostAsJsonAsync($"/bills/{billData.BillId}/items", new
        {
            description = "Bread",
            quantity = 1,
            amount = 0m,
            sharerParticipantIds = new[] { Guid.NewGuid().ToString() }
        });
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);
    }
}
