# Owezy — Project Status

## Project

Owezy — lightweight bill-splitting application.

## Architecture

Modular monolith. Single solution: `Owezy.slnx`.

Dependency direction:
```
Domain ← Application ← Infrastructure ← API
                                          ↑
                                     Owezy.Client
```

Architecture enforced by NetArchTest in `Owezy.ArchitectureTests`.

## Technology

- .NET 10 / C# / ASP.NET Core
- Entity Framework Core + SQL Server
- Tesseract OCR (local)
- Owezy.Client: Mobile-first Vanilla JS / HTML5 / CSS3 SPA served directly by `Owezy.Api`
- JSON: `JavaScriptEncoder.Default` (HTML-safe) for XSS defense-in-depth

## Endpoints

- `GET  /health` → `200 OK`
- `POST /auth/otp/request` → `202 Accepted`
- `POST /auth/otp/verify` → `200 OK`
- `POST /bills` → `201 Created` (JWT auth)
- `GET  /bills/{billId}` → `200 OK` (JWT auth, splitter — full bill summary)
- `POST /bills/{billId}/participants` → `200 OK` (JWT auth, splitter)
- `POST /bills/{billId}/items` → `201 Created` (JWT auth, splitter — sharers optional at create, enforced at finalize)
- `PUT  /bills/{billId}/items/{itemId}/sharers` → `200 OK` (JWT auth, splitter)
- `POST /bills/{billId}/finalize` → `200 OK` (JWT auth, splitter)
- `POST /bills/{billId}/participants/{participantId}/access-link` → `200 OK` (JWT auth, splitter)
- `GET  /bills/{billId}/payments` → `200 OK` (JWT auth, splitter)
- `GET  /bills/{billId}/settlement` → `200 OK` (JWT auth, splitter)
- `POST /bills/{billId}/receipt` → `201 Created` (JWT auth, splitter)
- `GET  /bills/{billId}/receipt/{receiptId}` → `200 OK` (JWT auth, splitter)
- `PUT  /bills/{billId}/receipt/{receiptId}` → `200 OK` (JWT auth, splitter)
- `POST /bills/{billId}/receipt/{receiptId}/confirm` → `200 OK` (JWT auth, splitter)
- `GET  /participant-access/{token}` → `200 OK` (AllowAnonymous)
- `GET  /participant-access/{token}/summary` → `200 OK` (AllowAnonymous)
- `POST /participant-access/{token}/payment` → `200 OK` (AllowAnonymous)

## Persistence

**Tables**: `OtpChallenges`, `Bills`, `BillParticipants`, `BillItems`, `BillItemSharers`, `ParticipantAccessLinks`, `Receipts`

## Completed Milestones

- **1.1–2.0.1** Auth + Bill/Participant/Items/Finalization foundation — COMPLETE
- **Participant Access & Sharing** — COMPLETE
- **Payment Tracking** — COMPLETE
- **Settlement & Final Balance** — COMPLETE
- **Receipt Capture & OCR Foundation** — COMPLETE
- **OCR Review & Confirmation** — COMPLETE
- **Sharer Assignment & Final Bill Composition** — COMPLETE
- **End-to-End Billing Consistency Audit & Hardening** — COMPLETE
- **API Contract & Error-Handling Hardening** — COMPLETE
- **MVP Production-Safety Audit** — COMPLETE
- **MVP Operational Readiness & Deployment Foundation** — COMPLETE
- **Final MVP Architecture & Readiness Audit** — COMPLETE
- **Bill & Participant Summary Views** — COMPLETE
- **Receipt/OCR → Billing Accuracy Hardening** — COMPLETE
- **End-to-End MVP User Journey Verification** — COMPLETE
- **MVP Scope & Technical Debt Cleanup** — COMPLETE
- **Comprehensive Security Assessment (Backend)** — COMPLETE
- **Final MVP Sign-Off & Handoff** — COMPLETE
- **Frontend & Mobile UX** — COMPLETE
- **Full Application Functional & Regression Testing** — COMPLETE
- **Full-System Security & Vulnerability Assessment** — COMPLETE
- **Production Deployment Preparation & Packaging** — COMPLETE

## Not Yet Implemented

UPI link generation, debt simplification, notifications, QR codes, payment gateways.
