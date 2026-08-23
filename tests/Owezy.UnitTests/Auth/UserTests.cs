using Owezy.Domain.Auth;
using Xunit;

namespace Owezy.UnitTests.Auth;

public class UserTests
{
    [Fact]
    public void Create_ValidPhoneNumber_InitializesUserWithActiveStatusAndUniqueId()
    {
        var phone = PhoneNumber.Create("+919876543210");
        var beforeCreation = DateTimeOffset.UtcNow;

        var user = User.Create(phone);
        var afterCreation = DateTimeOffset.UtcNow;

        Assert.NotNull(user);
        Assert.NotEqual(Guid.Empty, user.Id.Value);
        Assert.Equal(phone, user.PhoneNumber);
        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.InRange(user.CreatedAt, beforeCreation, afterCreation);
    }

    [Fact]
    public void Create_NullPhoneNumber_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => User.Create(null!));
    }

    [Fact]
    public void DisableAndEnable_UpdatesAccountStatusCorrectly()
    {
        var phone = PhoneNumber.Create("+919876543210");
        var user = User.Create(phone);

        user.Disable();
        Assert.Equal(AccountStatus.Disabled, user.Status);

        user.Enable();
        Assert.Equal(AccountStatus.Active, user.Status);
    }

    [Fact]
    public void Reconstitute_ValidState_ReconstructsUserEntity()
    {
        var userId = UserId.New();
        var phone = PhoneNumber.Create("+919876543210");
        var createdAt = DateTimeOffset.UtcNow.AddDays(-10);

        var user = User.Reconstitute(userId, phone, createdAt, AccountStatus.Disabled);

        Assert.Equal(userId, user.Id);
        Assert.Equal(phone, user.PhoneNumber);
        Assert.Equal(createdAt, user.CreatedAt);
        Assert.Equal(AccountStatus.Disabled, user.Status);
    }

    [Fact]
    public void Reconstitute_EmptyUserId_ThrowsArgumentException()
    {
        var phone = PhoneNumber.Create("+919876543210");
        Assert.Throws<ArgumentException>(() => User.Reconstitute(UserId.Empty, phone, DateTimeOffset.UtcNow, AccountStatus.Active));
    }
}
