using System.Text.RegularExpressions;

namespace Owezy.Domain.Auth;

public sealed partial class PhoneNumber : IEquatable<PhoneNumber>
{
    private static readonly Regex E164Regex = MyE164Regex();

    public string Value { get; }

    private PhoneNumber(string value)
    {
        Value = value;
    }

    public static PhoneNumber Create(string rawPhoneNumber)
    {
        if (string.IsNullOrWhiteSpace(rawPhoneNumber))
        {
            throw new ArgumentException("Phone number cannot be null or empty.", nameof(rawPhoneNumber));
        }

        var normalized = NormalizeRawString(rawPhoneNumber);

        if (!E164Regex.IsMatch(normalized))
        {
            throw new ArgumentException($"Invalid international phone number format: '{rawPhoneNumber}'. Must be valid E.164 (e.g. +919876543210).", nameof(rawPhoneNumber));
        }

        return new PhoneNumber(normalized);
    }

    public static bool TryCreate(string? rawPhoneNumber, out PhoneNumber? phoneNumber, out string? errorMessage)
    {
        phoneNumber = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(rawPhoneNumber))
        {
            errorMessage = "Phone number cannot be null or empty.";
            return false;
        }

        var normalized = NormalizeRawString(rawPhoneNumber);

        if (!E164Regex.IsMatch(normalized))
        {
            errorMessage = $"Invalid international phone number format: '{rawPhoneNumber}'. Must be valid E.164 (e.g. +919876543210).";
            return false;
        }

        phoneNumber = new PhoneNumber(normalized);
        return true;
    }

    public static string NormalizeRawString(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        // Trim leading and trailing whitespace
        var trimmed = raw.Trim();

        // Preserve leading '+' if present
        var hasPlusPrefix = trimmed.StartsWith('+');

        // Remove all non-digit characters
        var digitsOnly = new string(trimmed.Where(char.IsDigit).ToArray());

        return hasPlusPrefix ? "+" + digitsOnly : digitsOnly;
    }

    public bool Equals(PhoneNumber? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as PhoneNumber);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(PhoneNumber? left, PhoneNumber? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(PhoneNumber? left, PhoneNumber? right) => !(left == right);

    [GeneratedRegex(@"^\+[1-9]\d{6,14}$", RegexOptions.Compiled)]
    private static partial Regex MyE164Regex();
}
