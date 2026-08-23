# Owezy — Project Status

## Project Overview
- **Project Name**: Owezy
- **Description**: Lightweight bill-splitting application
- **Architecture**: Modular Monolith (.NET 10 C#, SQL Server, EF Core, Vanilla/Vite Client)
- **Solution File**: `Owezy.slnx`
- **Current Phase**: Phase 1 — Splitter Authentication
- **Current Milestone**: Milestone 1.2 — OTP Domain & Service Contracts (Hardened & Completed)

---

## Phase Progression Status

| Phase | Description | Status | Completion Gate |
|-------|-------------|--------|-----------------|
| **Phase 0** | Foundation Scaffolding & Governance | **COMPLETED** | Solution `Owezy.slnx` scaffolded, .ai rules, ADRs, specs & directional architecture tests passing |
| **Phase 1** | Splitter Authentication | **IN PROGRESS (1.1 & 1.2 COMPLETED)** | Milestones 1.1 & 1.2 verified; OTP persistence & JWT authentication flow pending |
| **Phase 2** | Bill Management Core | Pending | Manual bill creation, items & participant management backend + minimum bill UI |
| **Phase 3** | Advisory OCR Pipeline | Pending | OCR abstraction, hashing, caching & resilience backend + OCR review UI |
| **Phase 4** | Splitting Engine | Pending | Equal split claim logic & Largest Remainder Method backend + minimum claim/split UI |
| **Phase 5** | Participant Sharing & Privacy | Pending | Secure `/split/{billToken}/{participantToken}` scoped access backend + minimum participant UI |
| **Phase 6** | UPI Payments & Confirmation | Pending | `upi://pay` generation, mark paid, splitter confirmation backend + minimum payment UI |
| **Phase 7** | UI Consolidation, PWA & UX Hardening | Pending | Responsive refinement, PWA behavior, accessibility & visual polish |
| **Phase 8** | Security & Hardening | Pending | Rate limiting, token revocation, security headers, architecture tests |
| **Phase 9** | End-to-End Verification | Pending | E2E integration test suite clean execution |
| **Phase 10** | Post-v1 Architecture Review | Pending | Tech debt audit & final architectural verification |

---

## Major Decisions Log
- **Database**: MS SQL Server + EF Core (ADR-001)
- **Architecture**: Modular Monolith (.NET 10, `Owezy.slnx`) (ADR-002)
- **Splitter Auth**: Phone -> OTP -> JWT (ADR-003)
- **Participant Access**: Relationship Scoped Token Link `/split/{billToken}/{participantToken}` (ADR-004)
- **Participant Access Model**: Scoped read access + limited payment-status mutation (ADR-005)
- **OCR Strategy**: `IOcrService` abstraction, SHA-256 image hashing, SQL caching, architecture resilience requirements (ADR-006)
- **OTP Architecture**: `IOtpService` -> `ISmsProvider` -> `DevelopmentSmsProvider` + `ProductionSmsProvider`; HMAC-SHA-256 verifier via `HmacSha256OtpHasher` (ADR-007)
- **Monetary Precision**: `decimal` + Largest Remainder Method (ADR-008)
- **Payment Boundary**: UPI link generation & manual confirmation (ADR-009)
- **Item Splitting**: Equal division among claimers (ADR-010)

---

## Known Blockers / Issues
- None.

---

## Out-of-Scope Observations (Recorded for Firewall Enforcement)
- *Notifications / Reminders*: Rejected (Out of v1 scope)
- *Chat / Social*: Rejected (Out of v1 scope)
- *Analytics / Dashboards*: Rejected (Out of v1 scope)
- *Redis / Distributed Caching*: Rejected (SQL caching preferred for v1 simplicity)
