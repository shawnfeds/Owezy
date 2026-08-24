namespace Owezy.Domain.Billing;

public readonly record struct BillId(Guid Value)
{
    public static BillId New() => new(Guid.NewGuid());
    public static BillId Empty => new(Guid.Empty);

    public override string ToString() => Value.ToString();
}
