using Owezy.Domain.Auth;

namespace Owezy.Application.Auth;

public sealed class PhoneNumberNormalizer : IPhoneNumberNormalizer
{
    public PhoneNumber Normalize(string rawPhoneNumber)
    {
        return PhoneNumber.Create(rawPhoneNumber);
    }

    public bool TryNormalize(string? rawPhoneNumber, out PhoneNumber? phoneNumber, out string? errorMessage)
    {
        return PhoneNumber.TryCreate(rawPhoneNumber, out phoneNumber, out errorMessage);
    }
}
