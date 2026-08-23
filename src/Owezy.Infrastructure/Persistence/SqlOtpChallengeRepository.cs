using Microsoft.EntityFrameworkCore;
using Owezy.Application.Auth;
using Owezy.Domain.Auth;

namespace Owezy.Infrastructure.Persistence;

/// <summary>
/// SQL Server implementation of IOtpChallengeRepository.
/// Maps between OtpChallengeRow (EF/Infrastructure) and OtpChallenge (Domain).
/// EF types never leave this class.
/// </summary>
public sealed class SqlOtpChallengeRepository : IOtpChallengeRepository
{
    private readonly OwezyDbContext _context;

    public SqlOtpChallengeRepository(OwezyDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<OtpChallenge?> GetByIdAsync(
        OtpChallengeId id,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.OtpChallenges
            .AsTracking()
            .FirstOrDefaultAsync(r => r.Id == id.Value, cancellationToken);

        if (row is null)
        {
            return null;
        }

        return MapToDomain(row);
    }

    public async Task AddAsync(
        OtpChallenge challenge,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        var row = MapToRow(challenge);
        await _context.OtpChallenges.AddAsync(row, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        OtpChallenge challenge,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        var row = await _context.OtpChallenges
            .AsTracking()
            .FirstOrDefaultAsync(r => r.Id == challenge.Id.Value, cancellationToken);

        if (row is null)
        {
            throw new InvalidOperationException(
                $"OTP challenge {challenge.Id} was not found for update.");
        }

        // Update only mutable state — ImmutableFields (Id, PhoneNumber, OtpHash, CreatedAt, ExpiresAt)
        // are never changed by the domain after creation.
        row.RemainingAttempts = challenge.RemainingAttempts;
        row.State = (int)challenge.State;

        await _context.SaveChangesAsync(cancellationToken);
    }

    // ── Mapping ──────────────────────────────────────────────────────────────

    private static OtpChallengeRow MapToRow(OtpChallenge challenge)
    {
        return new OtpChallengeRow
        {
            Id = challenge.Id.Value,
            PhoneNumber = challenge.PhoneNumber.Value, // already canonical E.164
            OtpHash = challenge.OtpHash,
            CreatedAt = challenge.CreatedAt,
            ExpiresAt = challenge.ExpiresAt,
            RemainingAttempts = challenge.RemainingAttempts,
            State = (int)challenge.State
        };
    }

    private static OtpChallenge MapToDomain(OtpChallengeRow row)
    {
        return OtpChallenge.Reconstitute(
            id: new OtpChallengeId(row.Id),
            phoneNumber: PhoneNumber.Create(row.PhoneNumber),
            otpHash: row.OtpHash,
            createdAt: row.CreatedAt,
            expiresAt: row.ExpiresAt,
            remainingAttempts: row.RemainingAttempts,
            state: (OtpState)row.State
        );
    }
}
