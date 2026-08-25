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

namespace Owezy.IntegrationTests.Billing;

public class EndToEndBillingConsistencyAuditTests : IClassFixture<WebApplicationFactory<Program>>
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
                MerchantName = "Audit Diner",
                Total = 1200m,
                LineItems = new[]
                {
                    new OcrLineItem { Description = "Audit Feast", Quantity = 1m, UnitPrice = 1200m }
                }
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
    private readonly PhoneNumber _participantPhone = PhoneNumber.Create("+919123456789");

    public EndToEndBillingConsistencyAuditTests(WebApplicationFactory<Program> factory)
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

    [Fact]
    public async Task CompleteBillingLifecycle_EndToEndAudit_EnforcesAllInvariants()
    {
        var splitterClient = CreateAuthenticatedClient(_splitterPhone);

        // 1. Create Bill
        var createRes = await splitterClient.PostAsJsonAsync("/bills", new { title = "Audit Trip" });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();
        var billId = billData!.BillId;

        // 2. Add Participant
        var addPartRes = await splitterClient.PostAsJsonAsync($"/bills/{billId}/participants", new { phoneNumber = _participantPhone.Value });
        Assert.Equal(HttpStatusCode.OK, addPartRes.StatusCode);
        var partData = await addPartRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();
        var participantId = partData!.ParticipantId;

        // Get splitter participant ID from repository
        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billId)));
        var splitterPartId = bill!.Participants.First(p => p.PhoneNumber == _splitterPhone).Id.Value.ToString();

        // 3. Upload OCR Receipt
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var fileContent = new ByteArrayContent(jpegBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        using var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", "receipt.jpg");

        var uploadRes = await splitterClient.PostAsync($"/bills/{billId}/receipt", form);
        Assert.Equal(HttpStatusCode.Created, uploadRes.StatusCode);
        var uploadData = await uploadRes.Content.ReadFromJsonAsync<UploadReceiptHttpResponse>();

        // 4. Confirm OCR Receipt -> creates 0-sharer item
        var confirmRes = await splitterClient.PostAsync($"/bills/{billId}/receipt/{uploadData!.ReceiptId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        var confirmData = await confirmRes.Content.ReadFromJsonAsync<ConfirmReceiptHttpResponse>();
        var itemId = confirmData!.CreatedItemIds.Single();

        // 5. Attempt Finalization while item has 0 sharers -> MUST FAIL (409 Conflict)
        var prematureFinalizeRes = await splitterClient.PostAsync($"/bills/{billId}/finalize", null);
        Assert.Equal(HttpStatusCode.Conflict, prematureFinalizeRes.StatusCode);

        // 6. Assign Sharers to the item (both splitter and participant)
        var sharersRes = await splitterClient.PutAsJsonAsync($"/bills/{billId}/items/{itemId}/sharers", new
        {
            participantIds = new[] { splitterPartId, participantId }
        });
        Assert.Equal(HttpStatusCode.OK, sharersRes.StatusCode);

        // 7. Finalize Bill -> MUST SUCCEED
        var finalizeRes = await splitterClient.PostAsync($"/bills/{billId}/finalize", null);
        Assert.Equal(HttpStatusCode.OK, finalizeRes.StatusCode);

        // 8. Generate Access Link for Participant
        var linkRes = await splitterClient.PostAsync($"/bills/{billId}/participants/{participantId}/access-link", null);
        Assert.Equal(HttpStatusCode.OK, linkRes.StatusCode);
        var linkData = await linkRes.Content.ReadFromJsonAsync<GenerateAccessLinkHttpResponse>();

        // 9. Anonymous Participant accesses their link
        var anonClient = _factory.CreateClient();
        var partViewRes = await anonClient.GetAsync($"/participant-access/{linkData!.Token}");
        Assert.Equal(HttpStatusCode.OK, partViewRes.StatusCode);
        var partView = await partViewRes.Content.ReadFromJsonAsync<ParticipantBillViewHttpResponse>();
        Assert.NotNull(partView);
        Assert.Equal(600m, partView.TotalAmountOwed);
        Assert.Equal("Unpaid", partView.PaymentStatus);

        // 10. Participant marks self paid
        var markPaidRes = await anonClient.PostAsync($"/participant-access/{linkData.Token}/payment", null);
        Assert.Equal(HttpStatusCode.OK, markPaidRes.StatusCode);

        // 11. Splitter checks payments
        var paymentsRes = await splitterClient.GetAsync($"/bills/{billId}/payments");
        Assert.Equal(HttpStatusCode.OK, paymentsRes.StatusCode);
        var payments = await paymentsRes.Content.ReadFromJsonAsync<SplitterBillPaymentsHttpResponse>();
        Assert.NotNull(payments);
        var partPayment = payments.ParticipantPayments.First(p => p.ParticipantId == participantId);
        Assert.Equal("Paid", partPayment.PaymentStatus);

        // 12. Splitter checks settlement -> Exact money conservation
        var settlementRes = await splitterClient.GetAsync($"/bills/{billId}/settlement");
        Assert.Equal(HttpStatusCode.OK, settlementRes.StatusCode);
        var settlement = await settlementRes.Content.ReadFromJsonAsync<BillSettlementHttpResponse>();
        Assert.NotNull(settlement);
        Assert.Equal(1200m, settlement.TotalOwed);
        Assert.Equal(600m, settlement.TotalPaid);
        Assert.Equal(600m, settlement.TotalRemaining);
        Assert.Equal(settlement.TotalOwed, settlement.TotalPaid + settlement.TotalRemaining);

        // 13. Verify Finalization Immutability Invariants on finalized bill:
        // A. Adding participant -> 409
        var addPartFail = await splitterClient.PostAsJsonAsync($"/bills/{billId}/participants", new { phoneNumber = "+919000000000" });
        Assert.Equal(HttpStatusCode.Conflict, addPartFail.StatusCode);

        // B. Adding item -> 409
        var addItemFail = await splitterClient.PostAsJsonAsync($"/bills/{billId}/items", new
        {
            description = "Late Item",
            quantity = 1,
            amount = 100m,
            sharerParticipantIds = new[] { splitterPartId }
        });
        Assert.Equal(HttpStatusCode.Conflict, addItemFail.StatusCode);

        // C. Modifying sharers -> 409
        var modSharersFail = await splitterClient.PutAsJsonAsync($"/bills/{billId}/items/{itemId}/sharers", new
        {
            participantIds = new[] { splitterPartId }
        });
        Assert.Equal(HttpStatusCode.Conflict, modSharersFail.StatusCode);
    }
}
