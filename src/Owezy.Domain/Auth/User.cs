namespace Owezy.Domain.Auth;

public sealed class User
{
    public UserId Id { get; }
    public PhoneNumber PhoneNumber { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public AccountStatus Status { get; private set; }

    private User(UserId id, PhoneNumber phoneNumber, DateTimeOffset createdAt, AccountStatus status)
    {
        Id = id;
        PhoneNumber = phoneNumber ?? throw new ArgumentNullException(nameof(phoneNumber));
        CreatedAt = createdAt;
        Status = status;
    }

    public static User Create(PhoneNumber phoneNumber)
    {
        ArgumentNullException.ThrowIfNull(phoneNumber);

        return new User(
            UserId.New(),
            phoneNumber,
            DateTimeOffset.UtcNow,
            AccountStatus.Active
        );
    }

    public static User Reconstitute(UserId id, PhoneNumber phoneNumber, DateTimeOffset createdAt, AccountStatus status)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(phoneNumber);

        return new User(id, phoneNumber, createdAt, status);
    }

    public void Disable()
    {
        if (Status == AccountStatus.Disabled)
        {
            return;
        }

        Status = AccountStatus.Disabled;
    }

    public void Enable()
    {
        if (Status == AccountStatus.Active)
        {
            return;
        }

        Status = AccountStatus.Active;
    }
}
