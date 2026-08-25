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

public class ApiContractHardeningTests : IClassFixture<WebApplicationFactory<Program>>
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
                MerchantName = "Hardening Store",
                Total = 100m,
                LineItems = new[] { new OcrLineItem { Description = "Hardened Item", Quantity = 1m, UnitPrice = 100m } }
            });
        }
    }

    private class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private const string TestJwtKey = "api-test-jwt-signing-secret-key-32chars-long-12345";
    private readonly WebApplicationFactory<Program> _factory;
    private readonly PhoneNumber _splitterPhone = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _nonSplitterPhone = PhoneNumber.Create("+919123456789");

    public ApiContractHardeningTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Jwt:SigningKey", TestJwtKey);
            builder.UseSetting("Jwt:Issuer", "Owezy.Api");
            builder.UseSetting("Jwt:Audience", "Owezy.App");

            builder.ConfigureServices(services =>
            {
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

    [Theory]
    [InlineData("/bills", "POST")]
    [InlineData("/bills/00000000-0000-0000-0000-000000000000/participants", "POST")]
    [InlineData("/bills/00000000-0000-0000-0000-000000000000/items", "POST")]
    [InlineData("/bills/00000000-0000-0000-0000-000000000000/items/00000000-0000-0000-0000-000000000000/sharers", "PUT")]
    [InlineData("/bills/00000000-0000-0000-0000-000000000000/finalize", "POST")]
    [InlineData("/bills/00000000-0000-0000-0000-000000000000/receipt", "POST")]
    [InlineData("/bills/00000000-0000-0000-0000-000000000000/receipt/00000000-0000-0000-0000-000000000000", "GET")]
    [InlineData("/bills/00000000-0000-0000-0000-000000000000/payments", "GET")]
    [InlineData("/bills/00000000-0000-0000-0000-000000000000/settlement", "GET")]
    public async Task ProtectedEndpoints_Return401Unauthorized_WhenUnauthenticated(string path, string method)
    {
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(new HttpMethod(method), path);

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task InvalidGuidRouteParam_Returns400BadRequest()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);

        var res = await client.PostAsync("/bills/not-a-valid-guid/finalize", null);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var err = await res.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(err);
        Assert.Equal("invalid_bill_id", err.Code);
    }

    [Fact]
    public async Task NonExistentResource_Returns404NotFound()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);

        var res = await client.GetAsync($"/bills/{Guid.NewGuid()}/receipt/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task NonSplitter_AttemptingMutation_Returns403Forbidden()
    {
        var splitterClient = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await splitterClient.PostAsJsonAsync("/bills", new { title = "Hardening Bill" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var nonSplitterClient = CreateAuthenticatedClient(_nonSplitterPhone);
        var addPartRes = await nonSplitterClient.PostAsJsonAsync($"/bills/{billData!.BillId}/participants", new { phoneNumber = "+919000000000" });

        Assert.Equal(HttpStatusCode.Forbidden, addPartRes.StatusCode);
    }

    [Fact]
    public async Task FinalizedBill_AttemptingMutation_Returns409Conflict()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Hardening Bill" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var addPartRes = await client.PostAsJsonAsync($"/bills/{billData!.BillId}/participants", new { phoneNumber = _nonSplitterPhone.Value });
        var partData = await addPartRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billData.BillId)));
        var splitterPartId = bill!.Participants.First().Id.Value.ToString();

        await client.PostAsJsonAsync($"/bills/{billData.BillId}/items", new
        {
            description = "Item",
            quantity = 1,
            amount = 100m,
            sharerParticipantIds = new[] { splitterPartId }
        });

        // Finalize bill
        await client.PostAsync($"/bills/{billData.BillId}/finalize", null);

        // Attempting to add participant to finalized bill returns 409
        var res = await client.PostAsJsonAsync($"/bills/{billData.BillId}/participants", new { phoneNumber = "+919000000000" });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task ApiResponses_NeverExposeInternalExceptionsOrSecrets()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);

        // Intentionally query invalid endpoint / trigger error
        var res = await client.GetAsync($"/bills/{Guid.NewGuid()}/receipt/{Guid.NewGuid()}");
        var content = await res.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Exception", content);
        Assert.DoesNotContain("Stack", content);
        Assert.DoesNotContain("Owezy.Domain", content);
        Assert.DoesNotContain("JwtOptions", content);
        Assert.DoesNotContain("SigningKey", content);
    }
}
