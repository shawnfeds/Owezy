using Owezy.Domain.Auth;

namespace Owezy.Application.Auth;

public interface IOtpChallengeRepository
{
    Task<OtpChallenge?> GetByIdAsync(OtpChallengeId id, CancellationToken cancellationToken = default);
    Task AddAsync(OtpChallenge challenge, CancellationToken cancellationToken = default);
    Task UpdateAsync(OtpChallenge challenge, CancellationToken cancellationToken = default);
}
