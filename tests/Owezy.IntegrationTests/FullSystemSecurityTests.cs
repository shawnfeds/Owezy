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

/// <summary>
/// Full-System Security &amp; Vulnerability Assessment.
/// Tests auth, IDOR, participant isolation, billing logic attacks,
/// receipt/file security, injection surfaces, API abuse, and info disclosure.
/// </summary>
public class FullSystemSecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    // ── In-memory stubs ──────────────────────────────────────────────────────

    private class InMemoryOtpRepo : IOtpChallengeRepository
    {
        public Dictionary<OtpChallengeId, OtpChallenge> Store { get; } = new();
        public Task<OtpChallenge?> GetByIdAsync(OtpChallengeId id, CancellationToken ct = default)
        { Store.TryGetValue(id, out var c); return Task.FromResult(c); }
        public Task AddAsync(OtpChallenge c, CancellationToken ct = default)
        { Store[c.Id] = c; return Task.CompletedTask; }
        public Task UpdateAsync(OtpChallenge c, CancellationToken ct = default)
        { Store[c.Id] = c; return Task.CompletedTask; }
    }

    private class InMemoryBillRepo : IBillRepository
    {
        public Dictionary<BillId, Bill> Store { get; } = new();
        public Task<Bill?> GetByIdAsync(BillId id, CancellationToken ct = default)
        { Store.TryGetValue(id, out var b); return Task.FromResult(b); }
        public Task<Bill?> GetByAccessLinkHashAsync(string hash, CancellationToken ct = default)
        {
            var b = Store.Values.FirstOrDefault(x => x.AccessLinks.Any(l => l.TokenHash == hash && !l.IsRevoked));
            return Task.FromResult(b);
        }
        public Task AddAsync(Bill b, CancellationToken ct = default) { Store[b.Id] = b; return Task.CompletedTask; }
        public Task UpdateAsync(Bill b, CancellationToken ct = default) { Store[b.Id] = b; return Task.CompletedTask; }
    }

    private class InMemoryReceiptRepo : IReceiptRepository
    {
        public Dictionary<ReceiptId, Receipt> Store { get; } = new();
        public Task AddAsync(Receipt r, CancellationToken ct = default) { Store[r.Id] = r; return Task.CompletedTask; }
        public Task<Receipt?> GetByIdAsync(ReceiptId id, CancellationToken ct = default)
        { Store.TryGetValue(id, out var r); return Task.FromResult(r); }
        public Task UpdateAsync(Receipt r, CancellationToken ct = default) { Store[r.Id] = r; return Task.CompletedTask; }
    }

    private class InMemoryReceiptStorage : IReceiptStorage
    {
        public Task<string> StoreAsync(Stream s, string ext, CancellationToken ct = default)
            => Task.FromResult($"{Guid.NewGuid():N}.{ext}");
    }

    private class TestOcr : IOcrService
    {
        public Task<OcrReceiptDraft> ProcessAsync(Stream s, CancellationToken ct = default)
            => Task.FromResult(new OcrReceiptDraft
            {
                MerchantName = "<script>alert('xss')</script>",
                Total = 50m,
                LineItems = new[] { new OcrLineItem { Description = "<img src=x onerror=alert(1)>", Quantity = 1m, LineTotal = 50m } }
            });
    }

    private class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    private const string TestJwtKey = "full-system-security-jwt-key-32chars-long-xyz";
    private readonly WebApplicationFactory<Program> _factory;
    private readonly PhoneNumber _splitterA = PhoneNumber.Create("+919000000001");
    private readonly PhoneNumber _splitterB = PhoneNumber.Create("+919000000002");
    private readonly PhoneNumber _participantC = PhoneNumber.Create("+919000000003");

    public FullSystemSecurityTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Jwt:SigningKey", TestJwtKey);
            builder.UseSetting("Jwt:Issuer", "Owezy.Api");
            builder.UseSetting("Jwt:Audience", "Owezy.App");
            builder.UseSetting("OtpHasher:SecretKey", "full-system-otp-hasher-key-32chars-long-xyz");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IOtpChallengeRepository, InMemoryOtpRepo>();
                services.AddSingleton<IBillRepository, InMemoryBillRepo>();
                services.AddSingleton<IReceiptRepository, InMemoryReceiptRepo>();
                services.AddSingleton<IReceiptStorage, InMemoryReceiptStorage>();
                services.AddSingleton<IOcrService, TestOcr>();
                services.AddSingleton<IDateTimeProvider, FixedDateTimeProvider>();
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

    private HttpClient AuthClient(PhoneNumber phone)
    {
        var svc = _factory.Services.GetRequiredService<IAccessTokenService>();
        var tok = svc.GenerateAccessToken(phone);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tok.AccessToken);
        return client;
    }

    // ── 1. Authentication ─────────────────────────────────────────────────────

    [Fact]
    public async Task Auth_NoToken_ProtectedEndpoint_Returns401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync($"/bills/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Auth_MalformedJwt_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.real.jwt");
        var res = await client.GetAsync($"/bills/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Auth_WrongSigningKey_Returns401()
    {
        // Build a token with a DIFFERENT key
        var badKeyOpts = new JwtOptions
        {
            SigningKey = "completely-different-signing-key-32charsxyz",
            Issuer = "Owezy.Api",
            Audience = "Owezy.App",
            AccessTokenLifetimeMinutes = 15
        };
        var badSvc = new JwtAccessTokenService(badKeyOpts, new FixedDateTimeProvider());
        var badToken = badSvc.GenerateAccessToken(_splitterA);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", badToken.AccessToken);
        var res = await client.GetAsync($"/bills/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData("/bills", "POST")]
    [InlineData("/bills/00000000-0000-0000-0000-000000000000", "GET")]
    [InlineData("/bills/00000000-0000-0000-0000-000000000000/participants", "POST")]
    [InlineData("/bills/00000000-0000-0000-0000-000000000000/items", "POST")]
    [InlineData("/bills/00000000-0000-0000-0000-000000000000/finalize", "POST")]
    [InlineData("/bills/00000000-0000-0000-0000-000000000000/payments", "GET")]
    [InlineData("/bills/00000000-0000-0000-0000-000000000000/settlement", "GET")]
    public async Task Auth_AllProtectedEndpoints_Return401WhenUnauthenticated(string path, string method)
    {
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(new HttpMethod(method), path);
        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── 2. Authorization / IDOR ──────────────────────────────────────────────

    [Fact]
    public async Task IDOR_SplitterBCannotGetSplitterABillSummary_Returns403()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "A's Private Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var clientB = AuthClient(_splitterB);
        var res = await clientB.GetAsync($"/bills/{bill!.BillId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task IDOR_SplitterBCannotAddParticipantToSplitterABill_Returns403()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "A's Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var clientB = AuthClient(_splitterB);
        var res = await clientB.PostAsJsonAsync($"/bills/{bill!.BillId}/participants", new { phoneNumber = "+919111111111" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task IDOR_SplitterBCannotAddItemToSplitterABill_Returns403()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "A's Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var clientB = AuthClient(_splitterB);
        var res = await clientB.PostAsJsonAsync($"/bills/{bill!.BillId}/items", new
        {
            description = "Malicious Item",
            quantity = 1,
            amount = 100m
        });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task IDOR_SplitterBCannotFinalizeSplitterABill_Returns403()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "A's Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var clientB = AuthClient(_splitterB);
        var res = await clientB.PostAsync($"/bills/{bill!.BillId}/finalize", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task IDOR_SplitterBCannotGetPaymentsForSplitterABill_Returns403()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "A's Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        // Finalize so payment endpoint works
        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepo;
        var billObj = await repo!.GetByIdAsync(new BillId(Guid.Parse(bill!.BillId)));
        var splitterPartId = billObj!.Participants.First().Id.Value.ToString();

        await clientA.PostAsJsonAsync($"/bills/{bill.BillId}/items", new
        {
            description = "X",
            quantity = 1,
            amount = 10m,
            sharerParticipantIds = new[] { splitterPartId }
        });
        await clientA.PostAsync($"/bills/{bill.BillId}/finalize", null);

        var clientB = AuthClient(_splitterB);
        var res = await clientB.GetAsync($"/bills/{bill.BillId}/payments");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task IDOR_SplitterBCannotGetSettlementForSplitterABill_Returns403()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "A's Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepo;
        var billObj = await repo!.GetByIdAsync(new BillId(Guid.Parse(bill!.BillId)));
        var splitterPartId = billObj!.Participants.First().Id.Value.ToString();

        await clientA.PostAsJsonAsync($"/bills/{bill.BillId}/items", new
        {
            description = "X",
            quantity = 1,
            amount = 10m,
            sharerParticipantIds = new[] { splitterPartId }
        });
        await clientA.PostAsync($"/bills/{bill.BillId}/finalize", null);

        var clientB = AuthClient(_splitterB);
        var res = await clientB.GetAsync($"/bills/{bill.BillId}/settlement");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task IDOR_RandomBillIdReturns404NotFound()
    {
        var clientA = AuthClient(_splitterA);
        var res = await clientA.GetAsync($"/bills/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── 3. Participant Isolation ──────────────────────────────────────────────

    [Fact]
    public async Task ParticipantIsolation_TamperedToken_Returns404()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "Isolation Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var partRes = await clientA.PostAsJsonAsync($"/bills/{bill!.BillId}/participants",
            new { phoneNumber = _participantC.Value });
        var part = await partRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepo;
        var billObj = await repo!.GetByIdAsync(new BillId(Guid.Parse(bill.BillId)));
        var splitterPartId = billObj!.Participants.First(p => p.PhoneNumber == _splitterA).Id.Value.ToString();

        await clientA.PostAsJsonAsync($"/bills/{bill.BillId}/items", new
        {
            description = "Shared",
            quantity = 1,
            amount = 100m,
            sharerParticipantIds = new[] { part!.ParticipantId }
        });
        await clientA.PostAsync($"/bills/{bill.BillId}/finalize", null);

        var linkRes = await clientA.PostAsync(
            $"/bills/{bill.BillId}/participants/{part.ParticipantId}/access-link", null);
        var linkData = await linkRes.Content.ReadFromJsonAsync<GenerateAccessLinkHttpResponse>();

        // Tamper last 8 chars
        var token = linkData!.Token;
        var tampered = token[..^8] + "deadbeef";

        var anon = _factory.CreateClient();
        var viewRes = await anon.GetAsync($"/participant-access/{tampered}");
        Assert.Equal(HttpStatusCode.NotFound, viewRes.StatusCode);
    }

    [Fact]
    public async Task ParticipantIsolation_CorrectTokenOnlyShowsOwnItems()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "Multi-Part Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var partRes = await clientA.PostAsJsonAsync($"/bills/{bill!.BillId}/participants",
            new { phoneNumber = _participantC.Value });
        var part = await partRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepo;
        var billObj = await repo!.GetByIdAsync(new BillId(Guid.Parse(bill.BillId)));
        var splitterPartId = billObj!.Participants.First(p => p.PhoneNumber == _splitterA).Id.Value.ToString();

        // Add item shared only with splitter (NOT participant C)
        await clientA.PostAsJsonAsync($"/bills/{bill.BillId}/items", new
        {
            description = "Splitter-Only",
            quantity = 1,
            amount = 50m,
            sharerParticipantIds = new[] { splitterPartId }
        });

        // Add item shared with participant C
        await clientA.PostAsJsonAsync($"/bills/{bill.BillId}/items", new
        {
            description = "C's Item",
            quantity = 1,
            amount = 80m,
            sharerParticipantIds = new[] { part!.ParticipantId }
        });

        await clientA.PostAsync($"/bills/{bill.BillId}/finalize", null);

        var linkRes = await clientA.PostAsync(
            $"/bills/{bill.BillId}/participants/{part.ParticipantId}/access-link", null);
        var linkData = await linkRes.Content.ReadFromJsonAsync<GenerateAccessLinkHttpResponse>();

        var anon = _factory.CreateClient();
        var viewRes = await anon.GetAsync($"/participant-access/{linkData!.Token}");
        Assert.Equal(HttpStatusCode.OK, viewRes.StatusCode);

        var view = await viewRes.Content.ReadFromJsonAsync<ParticipantBillViewHttpResponse>();
        Assert.NotNull(view);

        // Participant C should only see their item, NOT the splitter-only item
        Assert.All(view!.Items, i => Assert.Equal("C's Item", i.Description));
        Assert.DoesNotContain(view.Items, i => i.Description == "Splitter-Only");

        // Amount owed should be 80 (not 130)
        Assert.Equal(80m, view.TotalAmountOwed);
    }

    [Fact]
    public async Task ParticipantIsolation_ParticipantCannotMarkOtherParticipantPaid()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "Payment Isolation Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var partRes = await clientA.PostAsJsonAsync($"/bills/{bill!.BillId}/participants",
            new { phoneNumber = _participantC.Value });
        var partC = await partRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();

        var partBRes = await clientA.PostAsJsonAsync($"/bills/{bill.BillId}/participants",
            new { phoneNumber = _splitterB.Value });
        var partB = await partBRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();

        await clientA.PostAsJsonAsync($"/bills/{bill.BillId}/items", new
        {
            description = "Shared",
            quantity = 1,
            amount = 100m,
            sharerParticipantIds = new[] { partC!.ParticipantId, partB!.ParticipantId }
        });
        await clientA.PostAsync($"/bills/{bill.BillId}/finalize", null);

        // Get participant C's token
        var linkCRes = await clientA.PostAsync(
            $"/bills/{bill.BillId}/participants/{partC.ParticipantId}/access-link", null);
        var linkC = await linkCRes.Content.ReadFromJsonAsync<GenerateAccessLinkHttpResponse>();

        // Participant C's token should only mark C as paid; it cannot mark B
        // Get participant B's token to verify B's status before
        var linkBRes = await clientA.PostAsync(
            $"/bills/{bill.BillId}/participants/{partB.ParticipantId}/access-link", null);
        var linkB = await linkBRes.Content.ReadFromJsonAsync<GenerateAccessLinkHttpResponse>();

        // Mark C as paid using C's token
        var anon = _factory.CreateClient();
        var markRes = await anon.PostAsync($"/participant-access/{linkC!.Token}/payment", null);
        Assert.Equal(HttpStatusCode.OK, markRes.StatusCode);

        // Verify B is still Unpaid using B's token
        var bView = await anon.GetAsync($"/participant-access/{linkB!.Token}");
        var bData = await bView.Content.ReadFromJsonAsync<ParticipantBillViewHttpResponse>();
        Assert.Equal("Unpaid", bData!.PaymentStatus);
    }

    // ── 4. Billing Business-Logic Attacks ─────────────────────────────────────

    [Fact]
    public async Task BillingAttack_NegativeAmount_Returns400()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "Attack Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var res = await clientA.PostAsJsonAsync($"/bills/{bill!.BillId}/items", new
        {
            description = "Negative",
            quantity = 1,
            amount = -100m
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task BillingAttack_ZeroQuantity_Returns400()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "Attack Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var res = await clientA.PostAsJsonAsync($"/bills/{bill!.BillId}/items", new
        {
            description = "Zero Qty",
            quantity = 0,
            amount = 10m
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task BillingAttack_CrossBillParticipantIdAsSharer_Returns400()
    {
        var clientA = AuthClient(_splitterA);

        // Create Bill 1 (gets participant from it)
        var bill1Res = await clientA.PostAsJsonAsync("/bills", new { title = "Bill 1" });
        var bill1 = await bill1Res.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepo;
        var bill1Obj = await repo!.GetByIdAsync(new BillId(Guid.Parse(bill1!.BillId)));
        var splitterParticipantId = bill1Obj!.Participants.First().Id.Value.ToString();

        // Create Bill 2 — try to use Bill 1's participant ID as sharer
        var bill2Res = await clientA.PostAsJsonAsync("/bills", new { title = "Bill 2" });
        var bill2 = await bill2Res.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var res = await clientA.PostAsJsonAsync($"/bills/{bill2!.BillId}/items", new
        {
            description = "Cross Bill",
            quantity = 1,
            amount = 50m,
            sharerParticipantIds = new[] { splitterParticipantId }
        });
        // Should be rejected since that participant ID belongs to bill1, not bill2
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task BillingAttack_ItemWithNoSharersCannotFinalize_Returns409()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "No Sharers Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        // Add item with no sharers
        await clientA.PostAsJsonAsync($"/bills/{bill!.BillId}/items", new
        {
            description = "Unassigned",
            quantity = 1,
            amount = 50m,
            sharerParticipantIds = Array.Empty<string>()
        });

        var res = await clientA.PostAsync($"/bills/{bill.BillId}/finalize", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task BillingAttack_ModifyFinalizedBill_Returns409()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "Finalized Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepo;
        var billObj = await repo!.GetByIdAsync(new BillId(Guid.Parse(bill!.BillId)));
        var splitterPartId = billObj!.Participants.First().Id.Value.ToString();

        await clientA.PostAsJsonAsync($"/bills/{bill.BillId}/items", new
        {
            description = "X",
            quantity = 1,
            amount = 10m,
            sharerParticipantIds = new[] { splitterPartId }
        });
        await clientA.PostAsync($"/bills/{bill.BillId}/finalize", null);

        // Try to add item post-finalization
        var addRes = await clientA.PostAsJsonAsync($"/bills/{bill.BillId}/items", new
        {
            description = "Post-Finalize",
            quantity = 1,
            amount = 10m,
            sharerParticipantIds = new[] { splitterPartId }
        });
        Assert.Equal(HttpStatusCode.Conflict, addRes.StatusCode);

        // Try to add participant post-finalization
        var partRes = await clientA.PostAsJsonAsync($"/bills/{bill.BillId}/participants",
            new { phoneNumber = "+919999999999" });
        Assert.Equal(HttpStatusCode.Conflict, partRes.StatusCode);
    }

    [Fact]
    public async Task BillingAttack_DoubleFinalization_Returns409()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "Double Fin Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepo;
        var billObj = await repo!.GetByIdAsync(new BillId(Guid.Parse(bill!.BillId)));
        var splitterPartId = billObj!.Participants.First().Id.Value.ToString();

        await clientA.PostAsJsonAsync($"/bills/{bill.BillId}/items", new
        {
            description = "X",
            quantity = 1,
            amount = 10m,
            sharerParticipantIds = new[] { splitterPartId }
        });
        await clientA.PostAsync($"/bills/{bill.BillId}/finalize", null);

        // Second finalization attempt
        var res = await clientA.PostAsync($"/bills/{bill.BillId}/finalize", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ── 5. Receipt / File Security ─────────────────────────────────────────────

    [Fact]
    public async Task ReceiptSecurity_InvalidMimeType_Returns400()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "Receipt Security Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        using var content = new MultipartFormDataContent();
        // text/plain is not allowed
        var bytes = "Not an image"u8.ToArray();
        var byteContent = new ByteArrayContent(bytes);
        byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(byteContent, "file", "test.txt");

        var res = await clientA.PostAsync($"/bills/{bill!.BillId}/receipt", content);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ReceiptSecurity_EmptyFile_Returns400()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "Receipt Empty Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        using var content = new MultipartFormDataContent();
        var byteContent = new ByteArrayContent(Array.Empty<byte>());
        byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(byteContent, "file", "empty.jpg");

        var res = await clientA.PostAsync($"/bills/{bill!.BillId}/receipt", content);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ReceiptSecurity_NoFile_Returns400()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "Receipt No File Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        using var content = new MultipartFormDataContent();
        var res = await clientA.PostAsync($"/bills/{bill!.BillId}/receipt", content);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ReceiptSecurity_CrossBillAccessDenied_Returns403Or404()
    {
        var clientA = AuthClient(_splitterA);
        var clientB = AuthClient(_splitterB);

        var billARes = await clientA.PostAsJsonAsync("/bills", new { title = "A Receipt Bill" });
        var billA = await billARes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        // B tries to get a receipt on A's bill
        var res = await clientB.GetAsync($"/bills/{billA!.BillId}/receipt/{Guid.NewGuid()}");

        // Should be 403 (unauthorized) or 404 (not found), never 200
        Assert.True(
            res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Expected 403 or 404 but got {res.StatusCode}"
        );
    }

    [Fact]
    public async Task ReceiptSecurity_UploadToFinalizedBill_Returns409()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "Finalized Receipt Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepo;
        var billObj = await repo!.GetByIdAsync(new BillId(Guid.Parse(bill!.BillId)));
        var splitterPartId = billObj!.Participants.First().Id.Value.ToString();

        await clientA.PostAsJsonAsync($"/bills/{bill.BillId}/items", new
        {
            description = "X",
            quantity = 1,
            amount = 10m,
            sharerParticipantIds = new[] { splitterPartId }
        });
        await clientA.PostAsync($"/bills/{bill.BillId}/finalize", null);

        // Try to upload receipt after finalization
        // Create a minimal valid JPEG header
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        using var content = new MultipartFormDataContent();
        var byteContent = new ByteArrayContent(jpegBytes);
        byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(byteContent, "file", "receipt.jpg");

        var res = await clientA.PostAsync($"/bills/{bill.BillId}/receipt", content);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ── 6. OCR XSS Output in API Response ─────────────────────────────────────

    [Fact]
    public async Task OcrOutput_ScriptTagsInResponse_AreReturnedAsPlainText_NotExecuted()
    {
        // The TestOcr stub returns XSS payloads. Verify the API returns them as
        // plain text (JSON-encoded strings), not as executable script.
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "XSS Test Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        using var content = new MultipartFormDataContent();
        var byteContent = new ByteArrayContent(jpegBytes);
        byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(byteContent, "file", "receipt.jpg");

        var uploadRes = await clientA.PostAsync($"/bills/{bill!.BillId}/receipt", content);
        Assert.Equal(HttpStatusCode.Created, uploadRes.StatusCode);

        var responseBody = await uploadRes.Content.ReadAsStringAsync();
        var contentTypeHeader = uploadRes.Content.Headers.ContentType?.MediaType;

        // Response must be JSON (not HTML)
        Assert.Equal("application/json", contentTypeHeader);

        // With JavaScriptEncoder.Default, < and > are Unicode-escaped.
        // The raw <script> tag must NOT appear literally in the JSON body.
        Assert.DoesNotContain("<script>", responseBody, StringComparison.OrdinalIgnoreCase);
        // It should appear as Unicode-escaped form (\u003c = <, \u003e = >)
        Assert.Contains("\\u003c", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    // ── 7. Input Validation ───────────────────────────────────────────────────

    [Fact]
    public async Task InputValidation_InvalidGuidBillId_Returns400()
    {
        var clientA = AuthClient(_splitterA);
        var res = await clientA.GetAsync("/bills/not-a-guid");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task InputValidation_InvalidPhoneNumber_Returns400()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "Validation Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var res = await clientA.PostAsJsonAsync($"/bills/{bill!.BillId}/participants",
            new { phoneNumber = "not-a-phone-number" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task InputValidation_EmptyBillTitle_Returns400()
    {
        var clientA = AuthClient(_splitterA);
        var res = await clientA.PostAsJsonAsync("/bills", new { title = "" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task InputValidation_MalformedJson_Returns400OrUnsupportedMediaType()
    {
        var clientA = AuthClient(_splitterA);
        var req = new HttpRequestMessage(HttpMethod.Post, "/bills");
        req.Content = new StringContent("{ invalid json }", System.Text.Encoding.UTF8, "application/json");
        var res = await clientA.SendAsync(req);

        Assert.True(
            res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnsupportedMediaType,
            $"Expected 400 or 415 but got {res.StatusCode}"
        );
    }

    // ── 8. Information Disclosure ─────────────────────────────────────────────

    [Fact]
    public async Task InfoDisclosure_ErrorResponsesDoNotExposeSecrets()
    {
        var clientA = AuthClient(_splitterA);
        var res = await clientA.GetAsync($"/bills/{Guid.NewGuid()}");
        var body = await res.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("StackTrace", body);
        Assert.DoesNotContain(TestJwtKey, body);
        Assert.DoesNotContain("SigningKey", body);
        Assert.DoesNotContain("ConnectionString", body);
        Assert.DoesNotContain("Owezy.Infrastructure", body);
        Assert.DoesNotContain("SqlServer", body);
    }

    [Fact]
    public async Task InfoDisclosure_401ResponseDoesNotExposeSecrets()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.token.here");
        var res = await client.GetAsync($"/bills/{Guid.NewGuid()}");
        var body = await res.Content.ReadAsStringAsync();

        Assert.DoesNotContain("SigningKey", body);
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain(TestJwtKey, body);
    }

    // ── 9. Participant Access Token Security ──────────────────────────────────

    [Fact]
    public async Task TokenSecurity_EmptyToken_Returns404()
    {
        var anon = _factory.CreateClient();
        var res = await anon.GetAsync("/participant-access/");
        // Could be 404 or 405 (no route matched)
        Assert.True(
            res.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected 404 or 405 but got {res.StatusCode}"
        );
    }

    [Fact]
    public async Task TokenSecurity_NonexistentToken_Returns404()
    {
        var anon = _factory.CreateClient();
        var fakeToken = Convert.ToHexStringLower(new byte[32]);
        var res = await anon.GetAsync($"/participant-access/{fakeToken}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task TokenSecurity_ParticipantAccessIsOnlyAvailableAfterFinalization()
    {
        // Create bill, generate access link BEFORE finalization — should return 404
        // because participant access requires finalized bill
        // (Note: GenerateAccessLink requires finalized bill at the domain level, so
        // we test that the domain enforces the invariant)
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "Pre-Finalize Token Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var partRes = await clientA.PostAsJsonAsync($"/bills/{bill!.BillId}/participants",
            new { phoneNumber = _participantC.Value });
        var part = await partRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();

        // Attempt access link before finalization — should be 409
        var linkRes = await clientA.PostAsync(
            $"/bills/{bill.BillId}/participants/{part!.ParticipantId}/access-link", null);
        Assert.Equal(HttpStatusCode.Conflict, linkRes.StatusCode);
    }

    // ── 10. AddBillItem without sharers is now valid (defect fix regression) ──

    [Fact]
    public async Task BillingFix_AddItemWithoutSharers_IsAllowed_SharersCanBeAssignedLater()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "Two-Step Item Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        // Add item without any sharers — should succeed now
        var itemRes = await clientA.PostAsJsonAsync($"/bills/{bill!.BillId}/items", new
        {
            description = "Unassigned Item",
            quantity = 1,
            amount = 50m,
            sharerParticipantIds = Array.Empty<string>()
        });
        Assert.Equal(HttpStatusCode.Created, itemRes.StatusCode);

        // Bill cannot be finalized yet because item has no sharers
        var finalRes = await clientA.PostAsync($"/bills/{bill.BillId}/finalize", null);
        Assert.Equal(HttpStatusCode.Conflict, finalRes.StatusCode);

        // Now assign a sharer (the splitter participant)
        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepo;
        var billObj = await repo!.GetByIdAsync(new BillId(Guid.Parse(bill.BillId)));
        var splitterPartId = billObj!.Participants.First().Id.Value.ToString();
        var itemObj = billObj.Items.First();

        var updateRes = await clientA.PutAsJsonAsync(
            $"/bills/{bill.BillId}/items/{itemObj.Id.Value}/sharers",
            new { participantIds = new[] { splitterPartId } });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        // Now finalization should succeed
        var finalRes2 = await clientA.PostAsync($"/bills/{bill.BillId}/finalize", null);
        Assert.Equal(HttpStatusCode.OK, finalRes2.StatusCode);
    }

    // ── 11. Concurrent / Replay Payment ──────────────────────────────────────

    [Fact]
    public async Task PaymentReplay_MarkPaidTwiceWithSameToken_IsIdempotentOrHandledGracefully()
    {
        var clientA = AuthClient(_splitterA);
        var billRes = await clientA.PostAsJsonAsync("/bills", new { title = "Replay Bill" });
        var bill = await billRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var partRes = await clientA.PostAsJsonAsync($"/bills/{bill!.BillId}/participants",
            new { phoneNumber = _participantC.Value });
        var part = await partRes.Content.ReadFromJsonAsync<AddParticipantHttpResponse>();

        await clientA.PostAsJsonAsync($"/bills/{bill.BillId}/items", new
        {
            description = "Item",
            quantity = 1,
            amount = 100m,
            sharerParticipantIds = new[] { part!.ParticipantId }
        });
        await clientA.PostAsync($"/bills/{bill.BillId}/finalize", null);

        var linkRes = await clientA.PostAsync(
            $"/bills/{bill.BillId}/participants/{part.ParticipantId}/access-link", null);
        var link = await linkRes.Content.ReadFromJsonAsync<GenerateAccessLinkHttpResponse>();

        var anon = _factory.CreateClient();
        var res1 = await anon.PostAsync($"/participant-access/{link!.Token}/payment", null);
        var res2 = await anon.PostAsync($"/participant-access/{link.Token}/payment", null);

        // Both responses should be 2xx (idempotent mark-as-paid)
        Assert.True(res1.IsSuccessStatusCode, $"First mark-paid failed: {res1.StatusCode}");
        Assert.True(res2.IsSuccessStatusCode, $"Second mark-paid failed: {res2.StatusCode}");

        // Payment status remains "Paid" after double mark
        var viewRes = await anon.GetAsync($"/participant-access/{link.Token}");
        var viewData = await viewRes.Content.ReadFromJsonAsync<ParticipantBillViewHttpResponse>();
        Assert.Equal("Paid", viewData!.PaymentStatus);
    }
}
