namespace Owezy.Domain.Auth;

public readonly record struct OtpChallengeId(Guid Value)
{
    public static OtpChallengeId New() => new(Guid.NewGuid());
    public static OtpChallengeId Empty => new(Guid.Empty);

    public override string ToString() => Value.ToString();
}
