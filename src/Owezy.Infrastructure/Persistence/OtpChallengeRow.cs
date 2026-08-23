namespace Owezy.Infrastructure.Persistence;

/// <summary>
/// EF Core persistence row for OTP challenges.
/// This is an Infrastructure-only type. It is never exposed through Application contracts.
/// </summary>
internal sealed class OtpChallengeRow
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string OtpHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int RemainingAttempts { get; set; }

    /// <summary>
    /// OtpState enum persisted as int.
    /// 1=Active, 2=Verified, 3=Expired, 4=Exhausted.
    /// </summary>
    public int State { get; set; }

    /// <summary>
    /// SQL Server rowversion for optimistic concurrency.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}
