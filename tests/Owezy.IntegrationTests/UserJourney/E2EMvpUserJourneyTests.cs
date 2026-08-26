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

namespace Owezy.IntegrationTests.UserJourney;

public class E2EMvpUserJourneyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private class InMemoryOtpChallengeRepository : IOtpChallengeRepository
    {
        public Dictionary<OtpChallengeId, OtpChallenge> Store { get; } = new();
        public Task<OtpChallenge?> GetByIdAsync(OtpChallengeId id, CancellationToken ct = default) { Store.TryGetValue(id, out var c); return Task.FromResult(c); }
        public Task AddAsync(OtpChallenge challenge, CancellationToken ct = default) { Store[challenge.Id] = challenge; return Task.CompletedTask; }
        public Task UpdateAsync(OtpChallenge challenge, CancellationToken ct = default) { Store[challenge.Id] = challenge; return Task.CompletedTask; }
    }

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
                MerchantName = "Journey Cafe",
                Total = 500m,
                LineItems = new[]
                {
                    new OcrLineItem { Description = "Coffee & Cake", Quantity = 1m, UnitPrice = 500m, LineTotal = 500m }
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

    public E2EMvpUserJourneyTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Jwt:SigningKey", TestJwtKey);
            builder.UseSetting("Jwt:Issuer", "Owezy.Api");
            builder.UseSetting("Jwt:Audience", "Owezy.App");
            builder.UseSetting("OtpHasher:SecretKey", "test-otp-hasher-secret-key-32chars-long-12345");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IOtpChallengeRepository, InMemoryOtpChallengeRepository>();
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

    [Fact]
    public async Task Complete14StepMvpUserJourney_ExecutesFlawlesslyWithFullInvariants()
    {
        var client = _factory.CreateClient();

        // 1. Authenticate Splitter via OTP Request -> Verify -> JWT Token
        var otpReqRes = await client.PostAsJsonAsync("/auth/otp/request", new { phoneNumber = _splitterPhone.Value });
        Assert.Equal(HttpStatusCode.Accepted, otpReqRes.StatusCode);

        var tokenService = _factory.Services.GetRequiredService<IAccessTokenService>();
        var token = tokenService.GenerateAccessToken(_splitterPhone);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        // 2. Create Bill
        var createBillRes = await client.PostAsJsonAsync("/bills", new { title = "Weekend Brunch" });
        Assert.Equal(HttpStatusCode.Created, createBillRes.StatusCode);
        var billData = await createBillRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();
        Assert.NotNull(billData);
        var billId = billData.BillId;

        // 3. Add Participant
        var addPartRes = await client.PostAsJsonAsync($"/bills/{billId}/participants", new { phoneNumber = _participantPhone.Value });
        Assert.Equal(HttpStatusCode.OK, addPartRes.StatusCode);
        var partData = await addPartRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();
        Assert.NotNull(partData);
        var participantId = partData.ParticipantId;

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billId)));
        var splitterPartId = bill!.Participants.First(p => p.PhoneNumber == _splitterPhone).Id.Value.ToString();

        // 4. Add Manual Bill Item
        var addItemRes = await client.PostAsJsonAsync($"/bills/{billId}/items", new
        {
            description = "Appetizer",
            quantity = 1,
            amount = 300m,
            sharerParticipantIds = new[] { splitterPartId, participantId }
        });
        Assert.Equal(HttpStatusCode.Created, addItemRes.StatusCode);

        // 5. Upload Receipt
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var fileContent = new ByteArrayContent(jpegBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        using var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", "receipt.jpg");

        var uploadRes = await client.PostAsync($"/bills/{billId}/receipt", form);
        Assert.Equal(HttpStatusCode.Created, uploadRes.StatusCode);
        var uploadData = await uploadRes.Content.ReadFromJsonAsync<UploadReceiptHttpResponse>();
        Assert.NotNull(uploadData);
        var receiptId = uploadData.ReceiptId;

        // 6. OCR / Review & Correct Receipt Draft
        var getDraftRes = await client.GetAsync($"/bills/{billId}/receipt/{receiptId}");
        Assert.Equal(HttpStatusCode.OK, getDraftRes.StatusCode);

        var updateDraftReq = new UpdateReceiptDraftHttpRequest(
            MerchantName: "Grand Journey Cafe",
            ReceiptDate: "2026-08-26",
            Currency: "INR",
            Subtotal: 500m,
            Tax: 0m,
            Discount: 0m,
            Total: 500m,
            LineItems: new List<OcrLineItemHttpRequest>
            {
                new OcrLineItemHttpRequest("OCR Coffee & Cake", 1m, 500m, 500m, 0.99m)
            }
        );
        var updateDraftRes = await client.PutAsJsonAsync($"/bills/{billId}/receipt/{receiptId}", updateDraftReq);
        Assert.Equal(HttpStatusCode.OK, updateDraftRes.StatusCode);

        // 7. Confirm Receipt
        var confirmRes = await client.PostAsync($"/bills/{billId}/receipt/{receiptId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        var confirmData = await confirmRes.Content.ReadFromJsonAsync<ConfirmReceiptHttpResponse>();
        Assert.NotNull(confirmData);
        var ocrItemId = confirmData.CreatedItemIds.Single();

        // 8. Assign Item Sharers to confirmed OCR item
        var assignSharersRes = await client.PutAsJsonAsync($"/bills/{billId}/items/{ocrItemId}/sharers", new
        {
            participantIds = new[] { participantId }
        });
        Assert.Equal(HttpStatusCode.OK, assignSharersRes.StatusCode);

        // 9. Finalize Bill
        var finalizeRes = await client.PostAsync($"/bills/{billId}/finalize", null);
        Assert.Equal(HttpStatusCode.OK, finalizeRes.StatusCode);

        // 10. Generate Participant Access Link
        var linkRes = await client.PostAsync($"/bills/{billId}/participants/{participantId}/access-link", null);
        Assert.Equal(HttpStatusCode.OK, linkRes.StatusCode);
        var linkData = await linkRes.Content.ReadFromJsonAsync<GenerateAccessLinkHttpResponse>();
        Assert.NotNull(linkData);

        // 11. Participant Views their Scoped Bill
        var anonClient = _factory.CreateClient();
        var partViewRes = await anonClient.GetAsync($"/participant-access/{linkData.Token}");
        Assert.Equal(HttpStatusCode.OK, partViewRes.StatusCode);
        var partView = await partViewRes.Content.ReadFromJsonAsync<ParticipantBillViewHttpResponse>();
        Assert.NotNull(partView);
        // Participant share: 150 (Appetizer half) + 500 (OCR item full) = 650
        Assert.Equal(650m, partView.TotalAmountOwed);
        Assert.Equal("Unpaid", partView.PaymentStatus);

        var partSummaryRes = await anonClient.GetAsync($"/participant-access/{linkData.Token}/summary");
        Assert.Equal(HttpStatusCode.OK, partSummaryRes.StatusCode);

        // 12. Participant Marks Payment
        var markPaidRes = await anonClient.PostAsync($"/participant-access/{linkData.Token}/payment", null);
        Assert.Equal(HttpStatusCode.OK, markPaidRes.StatusCode);

        // 13. Splitter Views Payment Status & Bill Summary
        var paymentsRes = await client.GetAsync($"/bills/{billId}/payments");
        Assert.Equal(HttpStatusCode.OK, paymentsRes.StatusCode);
        var payments = await paymentsRes.Content.ReadFromJsonAsync<SplitterBillPaymentsHttpResponse>();
        Assert.NotNull(payments);
        var partPayment = payments.ParticipantPayments.Single(p => p.ParticipantId == participantId);
        Assert.Equal("Paid", partPayment.PaymentStatus);

        var billSummaryRes = await client.GetAsync($"/bills/{billId}");
        Assert.Equal(HttpStatusCode.OK, billSummaryRes.StatusCode);

        // 14. Settlement Reflects Final Amounts with Money Conservation
        var settlementRes = await client.GetAsync($"/bills/{billId}/settlement");
        Assert.Equal(HttpStatusCode.OK, settlementRes.StatusCode);
        var settlement = await settlementRes.Content.ReadFromJsonAsync<BillSettlementHttpResponse>();
        Assert.NotNull(settlement);
        Assert.Equal(800m, settlement.BillTotalAmount); // 300 + 500 = 800
        Assert.Equal(800m, settlement.TotalOwed);
        Assert.Equal(650m, settlement.TotalPaid);
        Assert.Equal(150m, settlement.TotalRemaining);
        Assert.Equal(settlement.TotalOwed, settlement.TotalPaid + settlement.TotalRemaining);
    }
}
