namespace Owezy.Application.Auth;

public sealed record AccessTokenResult(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt
);
