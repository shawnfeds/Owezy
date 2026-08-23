using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Owezy.Infrastructure.Persistence;

internal sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallengeRow>
{
    public void Configure(EntityTypeBuilder<OtpChallengeRow> builder)
    {
        builder.ToTable("OtpChallenges");

        // Primary key
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("Id")
            .ValueGeneratedNever(); // The domain generates the GUID — EF must not override it.

        // Phone number: E.164 format, max 20 chars (e.g. +15555555555 is 12; +999999999999999 is 16).
        // nvarchar(20) is bounded and appropriate.
        builder.Property(x => x.PhoneNumber)
            .HasColumnName("PhoneNumber")
            .HasMaxLength(20)
            .IsRequired()
            .IsUnicode(true);

        // OTP verifier: HMAC-SHA-256 output = 32 bytes → hex-encoded = exactly 64 chars.
        // nchar(64) is the right fixed-length type.
        builder.Property(x => x.OtpHash)
            .HasColumnName("OtpHash")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired()
            .IsUnicode(false); // ASCII hex characters only.

        builder.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("ExpiresAt")
            .IsRequired();

        builder.Property(x => x.RemainingAttempts)
            .HasColumnName("RemainingAttempts")
            .IsRequired();

        // OtpState persisted as int.
        // Values: 1=Active, 2=Verified, 3=Expired, 4=Exhausted (see OtpState enum in Domain).
        builder.Property(x => x.State)
            .HasColumnName("State")
            .IsRequired();

        // Concurrency token: SQL Server rowversion.
        // Provides optimistic concurrency for state/attempt update operations.
        builder.Property(x => x.RowVersion)
            .HasColumnName("RowVersion")
            .IsRowVersion()
            .IsRequired();

        // Index: Primary-key lookup by Id is already covered by the clustered PK.
        // Index: Non-clustered index on PhoneNumber to support active-challenge lookup by phone.
        // Includes State and ExpiresAt as included columns to enable efficient filtering
        // without a secondary key lookup for the authentication workflow.
        builder.HasIndex(x => x.PhoneNumber)
            .HasDatabaseName("IX_OtpChallenges_PhoneNumber")
            .IsUnique(false);
    }
}
