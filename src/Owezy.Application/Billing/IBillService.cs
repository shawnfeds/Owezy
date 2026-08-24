using Owezy.Domain.Auth;

namespace Owezy.Application.Billing;

public interface IBillService
{
    Task<CreateBillResult> CreateBillAsync(PhoneNumber splitterPhoneNumber, CreateBillRequest request, CancellationToken cancellationToken = default);
    Task<AddParticipantResult> AddParticipantAsync(PhoneNumber callerPhoneNumber, AddParticipantRequest request, CancellationToken cancellationToken = default);
}
