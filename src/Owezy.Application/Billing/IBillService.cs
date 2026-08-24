using Owezy.Domain.Auth;

namespace Owezy.Application.Billing;

public interface IBillService
{
    Task<CreateBillResult> CreateBillAsync(PhoneNumber splitterPhoneNumber, CreateBillRequest request, CancellationToken cancellationToken = default);
    Task<AddParticipantResult> AddParticipantAsync(PhoneNumber callerPhoneNumber, AddParticipantRequest request, CancellationToken cancellationToken = default);
    Task<AddBillItemResult> AddBillItemAsync(PhoneNumber callerPhoneNumber, AddBillItemRequest request, CancellationToken cancellationToken = default);
    Task<CalculateItemSharesResult> CalculateItemSharesAsync(PhoneNumber callerPhoneNumber, CalculateItemSharesRequest request, CancellationToken cancellationToken = default);
}
