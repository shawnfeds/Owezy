# Handoff — Participant Access & Sharing Complete

## Current State

Participant Access & Sharing milestone is complete.

## Capabilities Implemented

- Domain Layer (`Owezy.Domain.Billing`):
  - `ParticipantAccessLink` aggregate entity.
  - `Bill.GenerateAccessLink(participantId, tokenHash, now)`: Enforces that access links can only be generated for FINALIZED bills and that the participant belongs to the bill. Revokes prior active links.
- Application Layer (`Owezy.Application.Billing`):
  - `IParticipantTokenGenerator` abstraction for secure token generation and SHA-256 hashing.
  - `GenerateParticipantAccessLinkAsync`: Authenticated splitter-only operation returning an unguessable raw token.
  - `GetParticipantViewAsync`: Anonymous participant view retrieval for finalized bills, using derived equal split shares without persisting shares.
- Infrastructure Layer (`Owezy.Infrastructure`):
  - `CryptoParticipantTokenGenerator` using `RandomNumberGenerator` (32 random bytes -> 64 hex chars) and `SHA256`.
  - `ParticipantAccessLinkRow` and `ParticipantAccessLinkConfiguration` with unique index on `TokenHash`.
  - EF Core migration `AddParticipantAccessLinks`.
- API Layer (`Owezy.Api.Billing`):
  - `POST /bills/{billId}/participants/{participantId}/access-link`: Splitter-only access link generation (`200 OK`).
  - `GET /participant-access/{token}`: Anonymous participant-scoped view (`200 OK` or `404 Not Found`).

## Verification

| Check | Result |
|---|---|
| Build | PASS (0 warnings, 0 errors) |
| Unit tests | 123/123 PASS |
| Architecture tests | 4/4 PASS |
| Integration & API tests | 42/42 PASS |
| Total test suite | 169/169 PASS |
| Working tree | CLEAN (after commit) |

## Security & Scope Boundary

- Raw tokens are NEVER persisted (only SHA-256 hashes are stored).
- Opaque random tokens contain no encoded business data or identifiers.
- Participant views are strictly participant-scoped (no cross-participant payment/status info).
- OPEN bills block participant access link generation and token view retrieval.
- Participant tokens cannot mutate bills, finalize bills, or access splitter endpoints.
- Payment tracking, settlement, OCR, UPI links, notifications, and QR codes are NOT implemented.

## Next

Wait for next explicit instruction.
