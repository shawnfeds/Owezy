using System.Security.Cryptography;
using System.Text;
using Owezy.Application.Billing;
using Owezy.Application.Common;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Xunit;

namespace Owezy.UnitTests.Billing;

public class SettlementServiceTests
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

    private class TestParticipantTokenGenerator : IParticipantTokenGenerator
    {
        public string GenerateToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        public string HashToken(string rawToken) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }

    private readonly InMemoryBillRepository _repo = new();
    private readonly TestDateTimeProvider _clock = new();
    private readonly TestParticipantTokenGenerator _tokenGen = new();
    private readonly PhoneNumber _splitterPhone = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _participantPhone = PhoneNumber.Create("+919123456789");
    private readonly PhoneNumber _otherPhone = PhoneNumber.Create("+919000000001");

    private BillService CreateService() => new(_repo, _clock, _tokenGen);

    private async Task<(BillService svc, BillId billId, ParticipantId splitterPartId, ParticipantId part2Id)> CreateFinalizedBillAsync()
    {
        var svc = CreateService();
        var createResult = await svc.CreateBillAsync(_splitterPhone, new CreateBillRequest("Settlement Test"));
        var addPartResult = await svc.AddParticipantAsync(_splitterPhone, new AddParticipantRequest(createResult.BillId, _participantPhone));

        var bill = await _repo.GetByIdAsync(createResult.BillId);
        var splitterPartId = bill!.Participants.First().Id;

        await svc.AddBillItemAsync(_splitterPhone, new AddBillItemRequest(
            createResult.BillId, "Meal", 1, 1200m,
            new[] { splitterPartId, addPartResult.ParticipantId }));

        await svc.FinalizeBillAsync(_splitterPhone, new FinalizeBillRequest(createResult.BillId));

        return (svc, createResult.BillId, splitterPartId, addPartResult.ParticipantId);
    }

    [Fact]
    public async Task AllUnpaid_RemainingEqualsTotalOwed()
    {
        var (svc, billId, _, _) = await CreateFinalizedBillAsync();

        var result = await svc.GetBillSettlementAsync(_splitterPhone, billId);

        Assert.Equal(0m, result.TotalPaid);
        Assert.Equal(result.TotalOwed, result.TotalRemaining);
        Assert.Equal(0, result.PaidCount);
        Assert.Equal(2, result.UnpaidCount);
    }

    [Fact]
    public async Task AllPaid_RemainingIsZero()
    {
        var (svc, billId, splitterPartId, part2Id) = await CreateFinalizedBillAsync();

        var link1 = await svc.GenerateParticipantAccessLinkAsync(_splitterPhone, new GenerateParticipantAccessLinkRequest(billId, splitterPartId));
        var link2 = await svc.GenerateParticipantAccessLinkAsync(_splitterPhone, new GenerateParticipantAccessLinkRequest(billId, part2Id));
        await svc.MarkParticipantPaidByTokenAsync(link1.RawToken);
        await svc.MarkParticipantPaidByTokenAsync(link2.RawToken);

        var result = await svc.GetBillSettlementAsync(_splitterPhone, billId);

        Assert.Equal(0m, result.TotalRemaining);
        Assert.Equal(result.TotalOwed, result.TotalPaid);
        Assert.Equal(2, result.PaidCount);
        Assert.Equal(0, result.UnpaidCount);
    }

    [Fact]
    public async Task SomePaid_RemainingEqualsUnpaidShares()
    {
        var (svc, billId, splitterPartId, part2Id) = await CreateFinalizedBillAsync();

        var link2 = await svc.GenerateParticipantAccessLinkAsync(_splitterPhone, new GenerateParticipantAccessLinkRequest(billId, part2Id));
        await svc.MarkParticipantPaidByTokenAsync(link2.RawToken);

        var result = await svc.GetBillSettlementAsync(_splitterPhone, billId);

        Assert.Equal(1, result.PaidCount);
        Assert.Equal(1, result.UnpaidCount);
        Assert.Equal(600m, result.TotalPaid);
        Assert.Equal(600m, result.TotalRemaining);
        Assert.Equal(1200m, result.TotalOwed);
    }

    [Fact]
    public async Task ExactMoneyConservation_TotalOwedEqualsPaidPlusRemaining()
    {
        var (svc, billId, splitterPartId, part2Id) = await CreateFinalizedBillAsync();

        var link1 = await svc.GenerateParticipantAccessLinkAsync(_splitterPhone, new GenerateParticipantAccessLinkRequest(billId, splitterPartId));
        await svc.MarkParticipantPaidByTokenAsync(link1.RawToken);

        var result = await svc.GetBillSettlementAsync(_splitterPhone, billId);

        Assert.Equal(result.TotalOwed, result.TotalPaid + result.TotalRemaining);
    }

    [Fact]
    public async Task PerParticipant_PaidAmount_IsCalculatedShare_WhenPaid()
    {
        var (svc, billId, _, part2Id) = await CreateFinalizedBillAsync();

        var link = await svc.GenerateParticipantAccessLinkAsync(_splitterPhone, new GenerateParticipantAccessLinkRequest(billId, part2Id));
        await svc.MarkParticipantPaidByTokenAsync(link.RawToken);

        var result = await svc.GetBillSettlementAsync(_splitterPhone, billId);

        var part2Settlement = result.Participants.First(p => p.ParticipantId == part2Id);
        Assert.Equal(600m, part2Settlement.AmountOwed);
        Assert.Equal(600m, part2Settlement.AmountPaid);
        Assert.Equal(0m, part2Settlement.AmountRemaining);
    }

    [Fact]
    public async Task PerParticipant_PaidAmount_IsZero_WhenUnpaid()
    {
        var (svc, billId, splitterPartId, _) = await CreateFinalizedBillAsync();

        var result = await svc.GetBillSettlementAsync(_splitterPhone, billId);

        var splitterSettlement = result.Participants.First(p => p.ParticipantId == splitterPartId);
        Assert.Equal(600m, splitterSettlement.AmountOwed);
        Assert.Equal(0m, splitterSettlement.AmountPaid);
        Assert.Equal(600m, splitterSettlement.AmountRemaining);
    }

    [Fact]
    public async Task OpenBill_Settlement_Throws()
    {
        var svc = CreateService();
        var createResult = await svc.CreateBillAsync(_splitterPhone, new CreateBillRequest("Open Bill"));
        var bill = await _repo.GetByIdAsync(createResult.BillId);
        var splitterPartId = bill!.Participants.First().Id;
        await svc.AddBillItemAsync(_splitterPhone, new AddBillItemRequest(createResult.BillId, "Item", 1, 100m, new[] { splitterPartId }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GetBillSettlementAsync(_splitterPhone, createResult.BillId));
    }

    [Fact]
    public async Task NonSplitter_Settlement_ThrowsUnauthorized()
    {
        var (svc, billId, _, _) = await CreateFinalizedBillAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetBillSettlementAsync(_participantPhone, billId));
    }

    [Fact]
    public async Task CrossBill_Settlement_ThrowsKeyNotFound()
    {
        var (svc, _, _, _) = await CreateFinalizedBillAsync();
        var foreignBillId = BillId.New();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.GetBillSettlementAsync(_splitterPhone, foreignBillId));
    }

    [Fact]
    public async Task Settlement_DoesNotMutatePaymentStatus()
    {
        var (svc, billId, _, _) = await CreateFinalizedBillAsync();

        // Call settlement multiple times
        await svc.GetBillSettlementAsync(_splitterPhone, billId);
        await svc.GetBillSettlementAsync(_splitterPhone, billId);
        var result = await svc.GetBillSettlementAsync(_splitterPhone, billId);

        // Payment status should still be unpaid — settlement is read-only
        Assert.All(result.Participants, p => Assert.Equal(PaymentStatus.Unpaid, p.PaymentStatus));
        Assert.Equal(0m, result.TotalPaid);
    }

    [Fact]
    public async Task Settlement_UsesExistingCalculator_RespectsLargestRemainder()
    {
        // 3 participants sharing a bill total that doesn't divide evenly
        var svc = CreateService();
        var thirdPhone = PhoneNumber.Create("+919111111111");
        var createResult = await svc.CreateBillAsync(_splitterPhone, new CreateBillRequest("Three-Way"));
        var p2 = await svc.AddParticipantAsync(_splitterPhone, new AddParticipantRequest(createResult.BillId, _participantPhone));
        var p3 = await svc.AddParticipantAsync(_splitterPhone, new AddParticipantRequest(createResult.BillId, thirdPhone));

        var bill = await _repo.GetByIdAsync(createResult.BillId);
        var splitterPartId = bill!.Participants.First().Id;

        // ₹100 among 3 → each gets ₹33.33 with one extra cent via largest-remainder
        await svc.AddBillItemAsync(_splitterPhone, new AddBillItemRequest(
            createResult.BillId, "Snacks", 1, 100m,
            new[] { splitterPartId, p2.ParticipantId, p3.ParticipantId }));

        await svc.FinalizeBillAsync(_splitterPhone, new FinalizeBillRequest(createResult.BillId));

        var result = await svc.GetBillSettlementAsync(_splitterPhone, createResult.BillId);

        // Money conservation must hold exactly
        Assert.Equal(result.TotalOwed, result.TotalPaid + result.TotalRemaining);
        // Total owed must sum to bill amount
        Assert.Equal(100m, result.TotalOwed);
    }
}
