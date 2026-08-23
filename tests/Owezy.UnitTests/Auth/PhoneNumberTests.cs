using Owezy.Application.Auth;
using Owezy.Domain.Auth;
using Xunit;

namespace Owezy.UnitTests.Auth;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("+91 98765 43210", "+919876543210")]
    [InlineData("+91-98765-43210", "+919876543210")]
    [InlineData("+91 (98765) 43210", "+919876543210")]
    [InlineData("+91.98765.43210", "+919876543210")]
    [InlineData("  +91 98765 43210  ", "+919876543210")]
    [InlineData("+1 415 555 2671", "+14155552671")]
    public void Normalize_ValidFormatting_ReturnsCanonicalE164String(string rawInput, string expectedCanonical)
    {
        var phone = PhoneNumber.Create(rawInput);

        Assert.NotNull(phone);
        Assert.Equal(expectedCanonical, phone.Value);
        Assert.Equal(expectedCanonical, phone.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("9876543210")] // Missing leading '+'
    [InlineData("invalid")]
    [InlineData("+123")] // Too short for valid E.164 (min 7 digits after +)
    [InlineData("+1234567890123456")] // Exceeds max 15 digits
    public void Create_InvalidInput_ThrowsArgumentException(string? invalidInput)
    {
        Assert.Throws<ArgumentException>(() => PhoneNumber.Create(invalidInput!));
    }

    [Fact]
    public void Equals_EquivalentFormatting_ReturnsTrue()
    {
        var phone1 = PhoneNumber.Create("+91 98765 43210");
        var phone2 = PhoneNumber.Create("+919876543210");
        var phone3 = PhoneNumber.Create("+91-98765-43210");

        Assert.Equal(phone1, phone2);
        Assert.Equal(phone2, phone3);
        Assert.True(phone1 == phone2);
        Assert.False(phone1 != phone2);
        Assert.Equal(phone1.GetHashCode(), phone2.GetHashCode());
    }

    [Fact]
    public void TryCreate_ValidInput_ReturnsTrueAndPhoneNumber()
    {
        var success = PhoneNumber.TryCreate("+91 98765 43210", out var phoneNumber, out var errorMessage);

        Assert.True(success);
        Assert.NotNull(phoneNumber);
        Assert.Null(errorMessage);
        Assert.Equal("+919876543210", phoneNumber!.Value);
    }

    [Fact]
    public void TryCreate_InvalidInput_ReturnsFalseAndErrorMessage()
    {
        var success = PhoneNumber.TryCreate("invalid", out var phoneNumber, out var errorMessage);

        Assert.False(success);
        Assert.Null(phoneNumber);
        Assert.NotNull(errorMessage);
    }

    [Fact]
    public void PhoneNumberNormalizer_Normalize_DelegatesToDomainPhoneNumber()
    {
        var normalizer = new PhoneNumberNormalizer();
        var phone = normalizer.Normalize("+91 98765 43210");

        Assert.NotNull(phone);
        Assert.Equal("+919876543210", phone.Value);
    }
}
