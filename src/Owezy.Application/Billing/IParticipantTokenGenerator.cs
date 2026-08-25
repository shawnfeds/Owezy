namespace Owezy.Application.Billing;

public interface IParticipantTokenGenerator
{
    /// <summary>Generates an unguessable, cryptographically secure random token string.</summary>
    string GenerateToken();

    /// <summary>Computes the secure one-way hash of the raw token for storage and lookup.</summary>
    string HashToken(string rawToken);
}
