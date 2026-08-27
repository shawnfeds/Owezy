# Current Task

## Status

COMPLETE

## Goal

Production Deployment Preparation & Packaging.

## Completed

- Configurable receipt storage path via `ReceiptStorage:RootPath` / `RECEIPT_STORAGE_ROOT` in `ServiceRegistration.cs`.
- Production-ready multi-stage `Dockerfile` with persistent storage volume mount `/app/receipts` and static asset packaging.
- Unified single-host/container deployment setup (Frontend + API served together by `Owezy.Api`).
- Production security configuration verified: environment-driven secrets, mandatory 32+ char JWT & OTP secret keys, exception disclosure suppressed in production, HTML-safe JSON output encoding.
- `docs/DEPLOYMENT.md` created with step-by-step SQL Server migration, Docker build/run, environment variables, health check (`/health`), and verification instructions.
- Complete 336-test suite passing cleanly.
