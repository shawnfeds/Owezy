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

public class SharerAssignmentApiTests : IClassFixture<WebApplicationFactory<Program>>
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

    public SharerAssignmentApiTests(WebApplicationFactory<Program> factory)
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

    [Fact]
    public async Task Splitter_UpdateItemSharers_Returns200OK()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Sharer Test Bill" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var addPartRes = await client.PostAsJsonAsync($"/bills/{billData!.BillId}/participants", new { phoneNumber = _participantPhone.Value });
        var partData = await addPartRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billData.BillId)));
        var splitterPartId = bill!.Participants.First().Id.Value.ToString();

        // Create item with splitter as sharer
        var addItemRes = await client.PostAsJsonAsync($"/bills/{billData.BillId}/items", new
        {
            description = "Pasta",
            quantity = 1,
            amount = 400m,
            sharerParticipantIds = new[] { splitterPartId }
        });
        var itemData = await addItemRes.Content.ReadFromJsonAsync<AddBillItemHttpResponse>();

        // Update item sharers to include both splitter and participant
        var putRes = await client.PutAsJsonAsync($"/bills/{billData.BillId}/items/{itemData!.ItemId}/sharers", new
        {
            participantIds = new[] { splitterPartId, partData!.ParticipantId }
        });

        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);
        var resData = await putRes.Content.ReadFromJsonAsync<UpdateItemSharersHttpResponse>();
        Assert.NotNull(resData);
        Assert.Equal(itemData.ItemId, resData.ItemId);
        Assert.Equal(2, resData.ParticipantIds.Count);
        Assert.Contains(splitterPartId, resData.ParticipantIds);
        Assert.Contains(partData.ParticipantId, resData.ParticipantIds);
    }

    [Fact]
    public async Task NonSplitter_UpdateItemSharers_Returns403Forbidden()
    {
        var splitterClient = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await splitterClient.PostAsJsonAsync("/bills", new { title = "Sharer Test Bill" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billData!.BillId)));
        var splitterPartId = bill!.Participants.First().Id.Value.ToString();

        var addItemRes = await splitterClient.PostAsJsonAsync($"/bills/{billData.BillId}/items", new
        {
            description = "Pasta",
            quantity = 1,
            amount = 400m,
            sharerParticipantIds = new[] { splitterPartId }
        });
        var itemData = await addItemRes.Content.ReadFromJsonAsync<AddBillItemHttpResponse>();

        var nonSplitterClient = CreateAuthenticatedClient(_participantPhone);
        var putRes = await nonSplitterClient.PutAsJsonAsync($"/bills/{billData.BillId}/items/{itemData!.ItemId}/sharers", new
        {
            participantIds = new[] { splitterPartId }
        });

        Assert.Equal(HttpStatusCode.Forbidden, putRes.StatusCode);
    }

    [Fact]
    public async Task CrossBillParticipant_Returns400BadRequest()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Sharer Test Bill" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billData!.BillId)));
        var splitterPartId = bill!.Participants.First().Id.Value.ToString();

        var addItemRes = await client.PostAsJsonAsync($"/bills/{billData.BillId}/items", new
        {
            description = "Pasta",
            quantity = 1,
            amount = 400m,
            sharerParticipantIds = new[] { splitterPartId }
        });
        var itemData = await addItemRes.Content.ReadFromJsonAsync<AddBillItemHttpResponse>();

        var putRes = await client.PutAsJsonAsync($"/bills/{billData.BillId}/items/{itemData!.ItemId}/sharers", new
        {
            participantIds = new[] { Guid.NewGuid().ToString() }
        });

        Assert.Equal(HttpStatusCode.BadRequest, putRes.StatusCode);
    }
}
