# Owezy — Project Status

## Project

Owezy — lightweight bill-splitting application.

## Architecture

Modular monolith. Single solution: `Owezy.slnx`.

Dependency direction:
```
Domain ← Application ← Infrastructure ← API
```

- `Domain`: zero external dependencies.
- `Application`: depends only on `Domain`. Must not depend on `Infrastructure`.
- `Infrastructure`: depends on `Application` + `Domain`. Must not depend on `API`.
- `API`: depends on `Application` and `Infrastructure` (composition root only).

## Technology

- .NET 10 / C# / ASP.NET Core
- Entity Framework Core
- SQL Server

## Authentication

OTP-based + JWT Access Token authentication.

Current OTP & JWT security, application workflow & HTTP API:
- Cryptographically secure 6-digit OTP
- 5-minute OTP expiration
- Max 5 failed OTP attempts
- HMAC-SHA-256 verifier with external HMAC secret
- Constant-time OTP verifier comparison
- Plaintext OTP never persisted or returned in HTTP responses
- JWT Access Token authentication:
  - Short-lived configurable lifetime (default: 15 mins)
  - HMAC-SHA-256 token signing with external configuration key
  - Claims: `sub` (canonical phone number), `phone_number`, `jti`, `iss`, `aud`
  - Complete ASP.NET Core JWT Bearer validation (issuer, audience, signing key, lifetime)
  - Refresh tokens are NOT implemented
- Application workflow: `RequestOtpAsync`, `VerifyOtpAsync`, `GenerateAccessToken`
- HTTP API Endpoints:
  - `POST /auth/otp/request` → `202 Accepted` (`{ "challengeId": "..." }`)
  - `POST /auth/otp/verify` → `200 OK` (`{ "accessToken": "...", "tokenType": "Bearer", "expiresAt": "..." }`)

## Persistence

**Table**: `OtpChallenges`

Key fields: `Id`, `PhoneNumber`, `OtpHash`, `CreatedAt`, `ExpiresAt`, `RemainingAttempts`, `State`, `RowVersion`

- SQL Server + EF Core. Repository in `Infrastructure`.
- Application contract: `IOtpChallengeRepository`
- Implementation: `SqlOtpChallengeRepository`
- Concurrency: `rowversion` optimistic concurrency token.

## Completed Milestones

- **1.1** Authentication Domain Foundation — COMPLETE
- **1.2** OTP Domain & Service Contracts — COMPLETE
- **1.3** OTP SQL Server Persistence — COMPLETE
- **1.4** OTP Authentication Workflow — COMPLETE
- **1.5** Authentication API Boundary — COMPLETE
- **1.6** Access Token Issuance — COMPLETE

## Current Milestone

**1.7** — see `CURRENT_TASK.md` for objective.

## Not Yet Implemented

Refresh tokens, token revocation, authorization policies/roles, user profiles, bill management, OCR, splitting engine, participant sharing, payments.

Do not expand into these areas unless explicitly instructed.
