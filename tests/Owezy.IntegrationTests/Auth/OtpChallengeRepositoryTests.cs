using Microsoft.EntityFrameworkCore;
using Owezy.Domain.Auth;
using Owezy.Infrastructure.Persistence;
using Xunit;

namespace Owezy.IntegrationTests.Auth;

/// <summary>
/// Persistence integration tests for OTP challenge repository and EF Core mapping.
///
/// IMPORTANT: These tests require a SQL Server instance accessible via LocalDB:
///   Server=(localdb)\mssqllocaldb;Database=Owezy_IntegrationTests;Trusted_Connection=True;
///
/// The tests are SKIPPED if SQL Server is unavailable.
/// They must NOT be replaced with SQLite or in-memory provider.
/// </summary>
public sealed class OtpChallengeRepositoryTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=Owezy_IntegrationTests;Trusted_Connection=True;MultipleActiveResultSets=true;";

    private OwezyDbContext _context = null!;
    private SqlOtpChallengeRepository _repository = null!;
    private bool _sqlServerAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            var options = new DbContextOptionsBuilder<OwezyDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;

            _context = new OwezyDbContext(options);
            await _context.Database.MigrateAsync();
            _sqlServerAvailable = true;
        }
        catch (Exception)
        {
            _sqlServerAvailable = false;
        }

        if (_sqlServerAvailable)
        {
            _repository = new SqlOtpChallengeRepository(_context);
        }
    }

    public async Task DisposeAsync()
    {
        if (_sqlServerAvailable && _context is not null)
        {
            // Clean up test data after each test class run.
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM OtpChallenges");
            await _context.DisposeAsync();
        }
    }

    private void SkipIfUnavailable()
    {
        if (!_sqlServerAvailable)
        {
            throw new SkipException("SQL Server (LocalDB) is not available in this environment. Persistence integration tests cannot run.");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static OtpChallenge CreateTestChallenge(string phone = "+919876543210")
    {
        var phoneNumber = PhoneNumber.Create(phone);
        var now = DateTimeOffset.UtcNow;
        return OtpChallenge.Create(phoneNumber, new string('A', 64), now);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Create + Retrieve: challenge round-trips correctly")]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsCorrectChallenge()
    {
        SkipIfUnavailable();

        var challenge = CreateTestChallenge();
        await _repository.AddAsync(challenge);

        var retrieved = await _repository.GetByIdAsync(challenge.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(challenge.Id, retrieved.Id);
        Assert.Equal(challenge.PhoneNumber, retrieved.PhoneNumber);
        Assert.Equal(challenge.OtpHash, retrieved.OtpHash);
        // Timestamps: compare to second precision (datetimeoffset has sub-second precision on SQL Server)
        Assert.Equal(challenge.CreatedAt.ToUnixTimeSeconds(), retrieved.CreatedAt.ToUnixTimeSeconds());
        Assert.Equal(challenge.ExpiresAt.ToUnixTimeSeconds(), retrieved.ExpiresAt.ToUnixTimeSeconds());
        Assert.Equal(challenge.RemainingAttempts, retrieved.RemainingAttempts);
        Assert.Equal(challenge.State, retrieved.State);
    }

    [Fact(DisplayName = "Phone normalization: canonical E.164 round-trips correctly")]
    public async Task AddAsync_PhoneNumber_PersistedAndReconstructedAsCanonicalE164()
    {
        SkipIfUnavailable();

        // Canonical form — as stored by domain
        var challenge = CreateTestChallenge("+919876543210");
        await _repository.AddAsync(challenge);

        var retrieved = await _repository.GetByIdAsync(challenge.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("+919876543210", retrieved.PhoneNumber.Value);
    }

    [Fact(DisplayName = "Retrieve missing challenge: returns null")]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        SkipIfUnavailable();

        var result = await _repository.GetByIdAsync(OtpChallengeId.New());

        Assert.Null(result);
    }

    [Fact(DisplayName = "Update: state and attempt changes are persisted")]
    public async Task UpdateAsync_StateAndAttemptChanges_ArePersisted()
    {
        SkipIfUnavailable();

        var hasher = new Owezy.Application.Auth.HmacSha256OtpHasher("integration-test-secret-key-12345");
        var phone = PhoneNumber.Create("+919876543210");
        var now = DateTimeOffset.UtcNow;
        var otp = "123456";
        var hash = hasher.HashOtp(otp);

        var challenge = OtpChallenge.Create(phone, hash, now);
        await _repository.AddAsync(challenge);

        // Simulate a failed attempt — this mutates RemainingAttempts and potentially State
        challenge.Verify(isHashMatch: false, now: now.AddSeconds(10));
        await _repository.UpdateAsync(challenge);

        // Re-fetch from a fresh context to avoid tracking cache
        await _context.DisposeAsync();
        var freshOptions = new DbContextOptionsBuilder<OwezyDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        _context = new OwezyDbContext(freshOptions);
        _repository = new SqlOtpChallengeRepository(_context);

        var refreshed = await _repository.GetByIdAsync(challenge.Id);

        Assert.NotNull(refreshed);
        Assert.Equal(4, refreshed.RemainingAttempts); // Started at 5, minus 1
        Assert.Equal(OtpState.Active, refreshed.State);
    }

    [Fact(DisplayName = "Lifecycle: Active state persists correctly")]
    public async Task AddAsync_ActiveState_IsPersistedCorrectly()
    {
        SkipIfUnavailable();

        var challenge = CreateTestChallenge();
        await _repository.AddAsync(challenge);

        var retrieved = await _repository.GetByIdAsync(challenge.Id);

        Assert.Equal(OtpState.Active, retrieved!.State);
    }

    [Fact(DisplayName = "Lifecycle: Verified state persists correctly after update")]
    public async Task UpdateAsync_VerifiedState_IsPersistedCorrectly()
    {
        SkipIfUnavailable();

        var hasher = new Owezy.Application.Auth.HmacSha256OtpHasher("integration-test-secret-key-12345");
        var phone = PhoneNumber.Create("+910000000001");
        var now = DateTimeOffset.UtcNow;
        var otp = "654321";
        var hash = hasher.HashOtp(otp);

        var challenge = OtpChallenge.Create(phone, hash, now);
        await _repository.AddAsync(challenge);

        var isMatch = hasher.VerifyHash(otp, challenge.OtpHash);
        challenge.Verify(isHashMatch: isMatch, now: now.AddSeconds(1));
        await _repository.UpdateAsync(challenge);

        await _context.DisposeAsync();
        var freshOptions = new DbContextOptionsBuilder<OwezyDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        _context = new OwezyDbContext(freshOptions);
        _repository = new SqlOtpChallengeRepository(_context);

        var retrieved = await _repository.GetByIdAsync(challenge.Id);
        Assert.Equal(OtpState.Verified, retrieved!.State);
    }

    [Fact(DisplayName = "RowVersion: concurrency token is mapped and non-null")]
    public async Task AddAsync_RowVersion_IsPopulatedByDatabase()
    {
        SkipIfUnavailable();

        var challenge = CreateTestChallenge("+910000000002");
        await _repository.AddAsync(challenge);

        // Verify the row exists and has a rowversion (we verify via raw query since RowVersion is not
        // exposed through the domain — this confirms the EF mapping is correct).
        var rowVersionExists = await _context.Database.ExecuteSqlRawAsync(
            "SELECT COUNT(*) FROM OtpChallenges WHERE Id = {0} AND RowVersion IS NOT NULL",
            challenge.Id.Value);

        // ExecuteSqlRawAsync returns rows affected; for SELECT COUNT(*) we do it differently:
        var count = await _context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM OtpChallenges WHERE Id = {challenge.Id.Value} AND RowVersion IS NOT NULL")
            .FirstOrDefaultAsync();

        Assert.Equal(1, count);
    }
}

/// <summary>
/// Used to skip a test when a runtime prerequisite is unavailable.
/// xUnit v2 does not natively support skip-at-runtime; this throws to mark the test as failed
/// but the reason is clearly reported in the output.
/// </summary>
internal sealed class SkipException : Exception
{
    public SkipException(string reason) : base(reason) { }
}
