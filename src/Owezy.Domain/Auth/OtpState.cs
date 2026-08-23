namespace Owezy.Domain.Auth;

public enum OtpState
{
    Active = 1,
    Verified = 2,
    Expired = 3,
    Exhausted = 4
}
