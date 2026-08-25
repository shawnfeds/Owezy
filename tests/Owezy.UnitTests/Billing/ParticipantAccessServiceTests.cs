using System.Security.Cryptography;
using System.Text;
using Owezy.Application.Billing;
using Owezy.Application.Common;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Xunit;

namespace Owezy.UnitTests.Billing;

public class ParticipantAccessServiceTests
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

    private class TestParticipantTokenGenerator : IParticipantTokenGenerator
    {
        public string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToHexStringLower(bytes);
        }

        public string HashToken(string rawToken)
        {
            var bytes = Encoding.UTF8.GetBytes(rawToken);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexStringLower(hash);
        }
    }

    private readonly InMemoryBillRepository _repo = new();
    private readonly TestDateTimeProvider _clock = new();
    private readonly TestParticipantTokenGenerator _tokenGenerator = new();
    private readonly PhoneNumber _splitterPhone = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _participantPhone = PhoneNumber.Create("+919123456789");

    private async Task<(BillService service, BillId billId, ParticipantId participantId)> CreateFinalizedBillAsync()
    {
        var svc = new BillService(_repo, _clock, _tokenGenerator);
        var createResult = await svc.CreateBillAsync(_splitterPhone, new CreateBillRequest("Dinner"));
        var addPartResult = await svc.AddParticipantAsync(_splitterPhone, new AddParticipantRequest(createResult.BillId, _participantPhone));
        var bill = await _repo.GetByIdAsync(createResult.BillId);
        var splitterPart = bill!.Participants.First();

        await svc.AddBillItemAsync(_splitterPhone, new AddBillItemRequest(
            createResult.BillId, "Steak", 1, 1000m, new[] { splitterPart.Id, addPartResult.ParticipantId }));

        await svc.FinalizeBillAsync(_splitterPhone, new FinalizeBillRequest(createResult.BillId));

        return (svc, createResult.BillId, addPartResult.ParticipantId);
    }

    [Fact]
    public async Task Splitter_CanGenerateParticipantAccessLink()
    {
        var (svc, billId, partId) = await CreateFinalizedBillAsync();

        var linkResult = await svc.GenerateParticipantAccessLinkAsync(
            _splitterPhone,
            new GenerateParticipantAccessLinkRequest(billId, partId));

        Assert.NotNull(linkResult);
        Assert.False(string.IsNullOrWhiteSpace(linkResult.RawToken));
        Assert.Equal(billId, linkResult.BillId);
        Assert.Equal(partId, linkResult.ParticipantId);
    }

    [Fact]
    public async Task NonSplitter_CannotGenerateParticipantAccessLink()
    {
        var (svc, billId, partId) = await CreateFinalizedBillAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GenerateParticipantAccessLinkAsync(
                _participantPhone,
                new GenerateParticipantAccessLinkRequest(billId, partId)));
    }

    [Fact]
    public async Task CannotGenerateLink_ForParticipantFromAnotherBill()
    {
        var (svc, billId, _) = await CreateFinalizedBillAsync();
        var foreignParticipantId = ParticipantId.New();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.GenerateParticipantAccessLinkAsync(
                _splitterPhone,
                new GenerateParticipantAccessLinkRequest(billId, foreignParticipantId)));
    }

    [Fact]
    public async Task CannotGenerateLink_ForOpenBill()
    {
        var svc = new BillService(_repo, _clock, _tokenGenerator);
        var createResult = await svc.CreateBillAsync(_splitterPhone, new CreateBillRequest("Open Bill"));
        var bill = await _repo.GetByIdAsync(createResult.BillId);
        var partId = bill!.Participants.First().Id;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GenerateParticipantAccessLinkAsync(
                _splitterPhone,
                new GenerateParticipantAccessLinkRequest(createResult.BillId, partId)));
    }

    [Fact]
    public async Task ValidToken_ReturnsParticipantScopedView()
    {
        var (svc, billId, partId) = await CreateFinalizedBillAsync();
        var linkResult = await svc.GenerateParticipantAccessLinkAsync(
            _splitterPhone,
            new GenerateParticipantAccessLinkRequest(billId, partId));

        var view = await svc.GetParticipantViewAsync(linkResult.RawToken);

        Assert.NotNull(view);
        Assert.Equal(billId, view.BillId);
        Assert.Equal("Dinner", view.BillTitle);
        Assert.Equal(1000m, view.BillTotalAmount);
        Assert.Equal(partId, view.ParticipantId);
        Assert.Equal(_participantPhone, view.ParticipantPhoneNumber);
        Assert.Equal(500m, view.TotalAmountOwed);
        Assert.Single(view.Items);
        Assert.Equal("Steak", view.Items[0].Description);
        Assert.Equal(500m, view.Items[0].MyShareAmount);
    }

    [Fact]
    public async Task InvalidToken_ReturnsNull()
    {
        var (svc, _, _) = await CreateFinalizedBillAsync();
        var view = await svc.GetParticipantViewAsync("invalid-random-token-12345");
        Assert.Null(view);
    }

    [Fact]
    public async Task TokenForParticipantA_DoesNotReturnParticipantBView()
    {
        var (svc, billId, partBId) = await CreateFinalizedBillAsync();
        var bill = await _repo.GetByIdAsync(billId);
        var partAId = bill!.Participants.First().Id;

        var linkA = await svc.GenerateParticipantAccessLinkAsync(_splitterPhone, new GenerateParticipantAccessLinkRequest(billId, partAId));
        var linkB = await svc.GenerateParticipantAccessLinkAsync(_splitterPhone, new GenerateParticipantAccessLinkRequest(billId, partBId));

        var viewA = await svc.GetParticipantViewAsync(linkA.RawToken);
        var viewB = await svc.GetParticipantViewAsync(linkB.RawToken);

        Assert.NotNull(viewA);
        Assert.NotNull(viewB);
        Assert.Equal(partAId, viewA.ParticipantId);
        Assert.Equal(partBId, viewB.ParticipantId);
        Assert.NotEqual(viewA.ParticipantId, viewB.ParticipantId);
    }
}
