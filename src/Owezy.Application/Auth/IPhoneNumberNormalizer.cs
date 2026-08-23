using Owezy.Domain.Auth;

namespace Owezy.Application.Auth;

public interface IPhoneNumberNormalizer
{
    PhoneNumber Normalize(string rawPhoneNumber);
    bool TryNormalize(string? rawPhoneNumber, out PhoneNumber? phoneNumber, out string? errorMessage);
}
