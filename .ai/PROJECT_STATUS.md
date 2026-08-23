# Owezy — Project Status

## Project Overview
- **Project Name**: Owezy
- **Description**: Lightweight bill-splitting application
- **Architecture**: Modular Monolith (.NET 8 C#, SQL Server, EF Core, Vanilla/Vite Client)
- **Current Phase**: Phase 0 — Foundation & Infrastructure
- **Current Milestone**: Milestone 0.1 — Baseline & Documentation Reconciliation

---

## Phase Progression Status

| Phase | Description | Status | Completion Gate |
|-------|-------------|--------|-----------------|
| **Phase 0** | Foundation & Infrastructure | **IN PROGRESS** | Solution scaffolded, .ai rules, ADRs, specs & tests ready |
| **Phase 1** | Splitter Authentication | Pending | Phone + OTP + JWT flow implemented & verified |
| **Phase 2** | Bill Management Core | Pending | Manual bill creation, items & participant management |
| **Phase 3** | Advisory OCR Pipeline | Pending | Azure OCR abstraction, hashing, caching & rate limiting |
| **Phase 4** | Splitting Engine | Pending | Equal split claim logic & Largest Remainder Method |
| **Phase 5** | Participant Sharing & Privacy | Pending | Secure `/split/{billToken}/{participantToken}` scoped access |
| **Phase 6** | UPI Payments & Confirmation | Pending | `upi://pay` generation, mark paid, splitter confirmation |
| **Phase 7** | Frontend Application | Pending | Mobile-first Splitter & Participant UI views |
| **Phase 8** | Security & Hardening | Pending | Rate limiting, token revocation, Architecture tests |
| **Phase 9** | End-to-End Verification | Pending | E2E integration test suite clean execution |
| **Phase 10** | Post-v1 Architecture Review | Pending | Tech debt audit & final architectural verification |

---

## Major Decisions Log
- **Database**: MS SQL Server + EF Core (ADR-001)
- **Architecture**: Modular Monolith (.NET 8) (ADR-002)
- **Splitter Auth**: Phone -> OTP -> JWT (ADR-003)
- **Participant Access**: Scoped Token Link `/split/{billToken}/{participantToken}` (ADR-004)
- **Participant Privacy**: Server-side authorization; participant sees ONLY their split (ADR-005)
- **OCR Cost Control**: Image hashing (SHA256), SQL caching, rate limiting (ADR-006)
- **OTP Architecture**: Dev OTP Provider + Prod SMS Provider abstraction (ADR-007)
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
