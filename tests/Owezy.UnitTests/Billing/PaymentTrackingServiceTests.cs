using System.Security.Cryptography;
using System.Text;
using Owezy.Application.Billing;
using Owezy.Application.Common;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Xunit;

namespace Owezy.UnitTests.Billing;

public class PaymentTrackingServiceTests
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
        var createResult = await svc.CreateBillAsync(_splitterPhone, new CreateBillRequest("Lunch"));
        var addPartResult = await svc.AddParticipantAsync(_splitterPhone, new AddParticipantRequest(createResult.BillId, _participantPhone));
        var bill = await _repo.GetByIdAsync(createResult.BillId);
        var splitterPart = bill!.Participants.First();

        await svc.AddBillItemAsync(_splitterPhone, new AddBillItemRequest(
            createResult.BillId, "Burger", 2, 800m, new[] { splitterPart.Id, addPartResult.ParticipantId }));

        await svc.FinalizeBillAsync(_splitterPhone, new FinalizeBillRequest(createResult.BillId));

        return (svc, createResult.BillId, addPartResult.ParticipantId);
    }

    [Fact]
    public async Task Participant_CanMarkSelfPaid_ViaToken()
    {
        var (svc, billId, partId) = await CreateFinalizedBillAsync();
        var linkResult = await svc.GenerateParticipantAccessLinkAsync(_splitterPhone, new GenerateParticipantAccessLinkRequest(billId, partId));

        var result = await svc.MarkParticipantPaidByTokenAsync(linkResult.RawToken);

        Assert.NotNull(result);
        Assert.Equal(partId, result.ParticipantId);
        Assert.Equal(PaymentStatus.Paid, result.PaymentStatus);
        Assert.NotNull(result.PaidAt);
    }

    [Fact]
    public async Task InvalidToken_MarkPaid_ReturnsNull()
    {
        var (svc, _, _) = await CreateFinalizedBillAsync();
        var result = await svc.MarkParticipantPaidByTokenAsync("invalid-token-12345");
        Assert.Null(result);
    }

    [Fact]
    public async Task MarkPaid_IsIdempotent_PreservesOriginalTimestamp()
    {
        var (svc, billId, partId) = await CreateFinalizedBillAsync();
        var linkResult = await svc.GenerateParticipantAccessLinkAsync(_splitterPhone, new GenerateParticipantAccessLinkRequest(billId, partId));

        _clock.UtcNow = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        var firstResult = await svc.MarkParticipantPaidByTokenAsync(linkResult.RawToken);

        _clock.UtcNow = new DateTimeOffset(2026, 8, 25, 13, 0, 0, TimeSpan.Zero);
        var secondResult = await svc.MarkParticipantPaidByTokenAsync(linkResult.RawToken);

        Assert.NotNull(secondResult);
        Assert.Equal(PaymentStatus.Paid, secondResult.PaymentStatus);
        Assert.Equal(firstResult!.PaidAt, secondResult.PaidAt);
    }

    [Fact]
    public async Task Splitter_CanGetGroupPaymentStatus()
    {
        var (svc, billId, partId) = await CreateFinalizedBillAsync();
        var linkResult = await svc.GenerateParticipantAccessLinkAsync(_splitterPhone, new GenerateParticipantAccessLinkRequest(billId, partId));
        await svc.MarkParticipantPaidByTokenAsync(linkResult.RawToken);

        var paymentsResult = await svc.GetSplitterBillPaymentsAsync(_splitterPhone, billId);

        Assert.NotNull(paymentsResult);
        Assert.Equal(billId, paymentsResult.BillId);
        Assert.Equal(800m, paymentsResult.BillTotalAmount);
        Assert.Equal(2, paymentsResult.ParticipantPayments.Count);

        var participantPay = paymentsResult.ParticipantPayments.First(p => p.ParticipantId == partId);
        Assert.Equal(400m, participantPay.AmountOwed);
        Assert.Equal(PaymentStatus.Paid, participantPay.PaymentStatus);
        Assert.NotNull(participantPay.PaidAt);

        var splitterPay = paymentsResult.ParticipantPayments.First(p => p.PhoneNumber == _splitterPhone);
        Assert.Equal(400m, splitterPay.AmountOwed);
        Assert.Equal(PaymentStatus.Unpaid, splitterPay.PaymentStatus);
    }

    [Fact]
    public async Task NonSplitter_CannotGetGroupPaymentStatus()
    {
        var (svc, billId, _) = await CreateFinalizedBillAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetSplitterBillPaymentsAsync(_participantPhone, billId));
    }

    [Fact]
    public async Task ParticipantView_IncludesOwnPaymentStatus()
    {
        var (svc, billId, partId) = await CreateFinalizedBillAsync();
        var linkResult = await svc.GenerateParticipantAccessLinkAsync(_splitterPhone, new GenerateParticipantAccessLinkRequest(billId, partId));

        var viewBefore = await svc.GetParticipantViewAsync(linkResult.RawToken);
        Assert.NotNull(viewBefore);
        Assert.Equal(PaymentStatus.Unpaid, viewBefore.PaymentStatus);

        await svc.MarkParticipantPaidByTokenAsync(linkResult.RawToken);

        var viewAfter = await svc.GetParticipantViewAsync(linkResult.RawToken);
        Assert.NotNull(viewAfter);
        Assert.Equal(PaymentStatus.Paid, viewAfter.PaymentStatus);
        Assert.NotNull(viewAfter.PaidAt);
    }
}
