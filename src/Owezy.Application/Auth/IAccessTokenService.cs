using Owezy.Domain.Auth;

namespace Owezy.Application.Auth;

public interface IAccessTokenService
{
    AccessTokenResult GenerateAccessToken(PhoneNumber phoneNumber);
}
