namespace Owezy.Domain.Billing;

public readonly record struct ParticipantId(Guid Value)
{
    public static ParticipantId New() => new(Guid.NewGuid());
    public static ParticipantId Empty => new(Guid.Empty);

    public override string ToString() => Value.ToString();
}
