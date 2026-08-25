using Owezy.Domain.Auth;
using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public interface IBillService
{
    Task<CreateBillResult> CreateBillAsync(PhoneNumber splitterPhoneNumber, CreateBillRequest request, CancellationToken cancellationToken = default);
    Task<AddParticipantResult> AddParticipantAsync(PhoneNumber callerPhoneNumber, AddParticipantRequest request, CancellationToken cancellationToken = default);
    Task<AddBillItemResult> AddBillItemAsync(PhoneNumber callerPhoneNumber, AddBillItemRequest request, CancellationToken cancellationToken = default);
    Task<CalculateItemSharesResult> CalculateItemSharesAsync(PhoneNumber callerPhoneNumber, CalculateItemSharesRequest request, CancellationToken cancellationToken = default);
    Task<FinalizeBillResult> FinalizeBillAsync(PhoneNumber callerPhoneNumber, FinalizeBillRequest request, CancellationToken cancellationToken = default);
    Task<GenerateParticipantAccessLinkResult> GenerateParticipantAccessLinkAsync(PhoneNumber callerPhoneNumber, GenerateParticipantAccessLinkRequest request, CancellationToken cancellationToken = default);
    Task<ParticipantBillViewResult?> GetParticipantViewAsync(string rawToken, CancellationToken cancellationToken = default);
    Task<MarkParticipantPaidResult?> MarkParticipantPaidByTokenAsync(string rawToken, CancellationToken cancellationToken = default);
    Task<SplitterBillPaymentsResult> GetSplitterBillPaymentsAsync(PhoneNumber callerPhoneNumber, BillId billId, CancellationToken cancellationToken = default);
    Task<BillSettlementResult> GetBillSettlementAsync(PhoneNumber callerPhoneNumber, BillId billId, CancellationToken cancellationToken = default);
}
