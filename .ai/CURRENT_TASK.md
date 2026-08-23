# Current Active Task — Phase 1 / Milestone 1.2: OTP Domain & Service Contracts (Security Finalized)

## Objective
Finalize OTP security hardening: remove hardcoded secrets, rename implementation to `HmacSha256OtpHasher`, enforce constant-time byte comparison (`CryptographicOperations.FixedTimeEquals`), and update test suites.

## Active Scope & Tasks
- [x] Remove default hardcoded secret string from `OtpHasherOptions`
- [x] Rename `Sha256OtpHasher` $\rightarrow$ `HmacSha256OtpHasher`
- [x] Add fail-fast validation (`InvalidOperationException`) when `SecretKey` is missing/whitespace
- [x] Implement constant-time byte array comparison via `CryptographicOperations.FixedTimeEquals`
- [x] Add unit tests for fail-fast options, malformed verifiers, leading-zero OTPs, and key isolation
- [x] Source & config scan: 0 hardcoded secrets in source files or `appsettings*.json`
- [x] Solution compilation & architecture test verification (54 tests passing)
- [x] Update status & handoff documentation

## Status
**Completed**. Ready for review. HARD STOP condition reached.
