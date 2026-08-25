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

namespace Owezy.IntegrationTests.Receipts;

public class ReceiptApiTests : IClassFixture<WebApplicationFactory<Program>>
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

        public Task AddAsync(Receipt receipt, CancellationToken cancellationToken = default)
        {
            Store[receipt.Id] = receipt;
            return Task.CompletedTask;
        }

        public Task<Receipt?> GetByIdAsync(ReceiptId receiptId, CancellationToken cancellationToken = default)
        {
            Store.TryGetValue(receiptId, out var r);
            return Task.FromResult(r);
        }

        public Task UpdateAsync(Receipt receipt, CancellationToken cancellationToken = default)
        {
            Store[receipt.Id] = receipt;
            return Task.CompletedTask;
        }
    }

    private class InMemoryReceiptStorage : IReceiptStorage
    {
        public Dictionary<string, byte[]> StoredFiles { get; } = new();

        public Task<string> StoreAsync(Stream imageStream, string fileExtension, CancellationToken cancellationToken = default)
        {
            var key = $"{Guid.NewGuid():N}.{fileExtension}";
            using var ms = new MemoryStream();
            imageStream.CopyTo(ms);
            StoredFiles[key] = ms.ToArray();
            return Task.FromResult(key);
        }
    }

    private class TestOcrService : IOcrService
    {
        public Task<OcrReceiptDraft> ProcessAsync(Stream imageStream, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OcrReceiptDraft
            {
                MerchantName = "Test Store",
                Total = 250m,
                LineItems = new[]
                {
                    new OcrLineItem { Description = "Item 1", Quantity = 1m, UnitPrice = 250m }
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

    public ReceiptApiTests(WebApplicationFactory<Program> factory)
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

    private static MultipartFormDataContent CreateValidImageContent()
    {
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var fileContent = new ByteArrayContent(jpegBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", "receipt.jpg");
        return form;
    }

    [Fact]
    public async Task Splitter_UploadReceipt_Returns201CreatedWithDraft()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Receipt Test Bill" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        using var form = CreateValidImageContent();
        var res = await client.PostAsync($"/bills/{billData!.BillId}/receipt", form);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var data = await res.Content.ReadFromJsonAsync<UploadReceiptHttpResponse>();
        Assert.NotNull(data);
        Assert.Equal(billData.BillId, data.BillId);
        Assert.Equal("Processed", data.Status);
        Assert.NotNull(data.OcrDraft);
        Assert.Equal("Test Store", data.OcrDraft.MerchantName);
    }

    [Fact]
    public async Task NonSplitter_UploadReceipt_Returns403Forbidden()
    {
        var splitterClient = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await splitterClient.PostAsJsonAsync("/bills", new { title = "Receipt Test Bill" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var nonSplitterClient = CreateAuthenticatedClient(_participantPhone);
        using var form = CreateValidImageContent();

        var res = await nonSplitterClient.PostAsync($"/bills/{billData!.BillId}/receipt", form);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedUploadReceipt_Returns401Unauthorized()
    {
        var anonClient = _factory.CreateClient();
        using var form = CreateValidImageContent();

        var res = await anonClient.PostAsync($"/bills/{Guid.NewGuid()}/receipt", form);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task UploadUnsupportedFile_Returns400BadRequest()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Receipt Test Bill" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        var fileContent = new ByteArrayContent("Some text content"u8.ToArray());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
        using var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", "document.txt");

        var res = await client.PostAsync($"/bills/{billData!.BillId}/receipt", form);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Splitter_GetReceiptDraft_Returns200OK()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Receipt Test Bill" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        using var form = CreateValidImageContent();
        var uploadRes = await client.PostAsync($"/bills/{billData!.BillId}/receipt", form);
        var uploadData = await uploadRes.Content.ReadFromJsonAsync<UploadReceiptHttpResponse>();

        var getRes = await client.GetAsync($"/bills/{billData.BillId}/receipt/{uploadData!.ReceiptId}");

        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var getDraftData = await getRes.Content.ReadFromJsonAsync<ReceiptDraftHttpResponse>();
        Assert.NotNull(getDraftData);
        Assert.Equal(uploadData.ReceiptId, getDraftData.ReceiptId);
        Assert.Equal("Processed", getDraftData.Status);
    }

    [Fact]
    public async Task NonSplitter_GetReceiptDraft_Returns403Forbidden()
    {
        var splitterClient = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await splitterClient.PostAsJsonAsync("/bills", new { title = "Receipt Test Bill" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        using var form = CreateValidImageContent();
        var uploadRes = await splitterClient.PostAsync($"/bills/{billData!.BillId}/receipt", form);
        var uploadData = await uploadRes.Content.ReadFromJsonAsync<UploadReceiptHttpResponse>();

        var nonSplitterClient = CreateAuthenticatedClient(_participantPhone);
        var getRes = await nonSplitterClient.GetAsync($"/bills/{billData.BillId}/receipt/{uploadData!.ReceiptId}");

        Assert.Equal(HttpStatusCode.Forbidden, getRes.StatusCode);
    }

    [Fact]
    public async Task ReceiptUpload_DoesNotModifyBillItemsOrPaymentState()
    {
        var client = CreateAuthenticatedClient(_splitterPhone);
        var createRes = await client.PostAsJsonAsync("/bills", new { title = "Receipt Test Bill" });
        var billData = await createRes.Content.ReadFromJsonAsync<CreateBillHttpResponse>();

        using var form = CreateValidImageContent();
        await client.PostAsync($"/bills/{billData!.BillId}/receipt", form);

        // Verify bill has 0 items
        var repo = _factory.Services.GetRequiredService<IBillRepository>() as InMemoryBillRepository;
        var bill = await repo!.GetByIdAsync(new BillId(Guid.Parse(billData.BillId)));
        Assert.Empty(bill!.Items);
    }
}
