# Owezy — Production Deployment Guide

Concise guide for deploying the unified Owezy container (Frontend + ASP.NET Core API) with SQL Server.

---

## Prerequisites

- **Docker Engine** 20.10+ or **Container Runtime**
- **SQL Server** 2019+ (or Azure SQL Database / Docker SQL Server container)
- Valid **E.164 phone numbers** for splitters/participants

---

## Required Environment Variables

| Variable | Description | Example / Requirement |
|---|---|---|
| `ConnectionStrings__OwezyDb` | SQL Server connection string | `Server=sql-host;Database=OwezyDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True;` |
| `Jwt__SigningKey` | JWT Signing Secret Key | **MUST be >= 32 characters** (e.g. `your-production-jwt-signing-secret-key-32-chars-min`) |
| `Jwt__Issuer` | JWT Token Issuer | `Owezy.Api` |
| `Jwt__Audience` | JWT Token Audience | `Owezy.App` |
| `OtpHasher__SecretKey` | HMAC Secret for hashing OTPs | **MUST be >= 32 characters** (e.g. `your-production-otp-hasher-secret-key-32-chars-min`) |
| `RECEIPT_STORAGE_ROOT` | Path for uploaded receipt images | `/app/receipts` (Mount Docker volume here) |
| `ASPNETCORE_ENVIRONMENT` | Environment mode | `Production` |

---

## SQL Server Database Migration Procedure

Owezy uses EF Core migrations. Execute migrations prior to app launch or via EF Core CLI:

```bash
# Apply EF Core migrations to target database
dotnet ef database update --project src/Owezy.Infrastructure --startup-project src/Owezy.Api
```

---

## Receipt Persistent Storage

Uploaded receipts are stored locally on disk under `RECEIPT_STORAGE_ROOT`.

- Mount a persistent volume to `/app/receipts` to prevent data loss across container restarts.

---

## Docker Commands

### 1. Build Production Image

```bash
docker build -t owezy:latest .
```

### 2. Run Container

```bash
docker run -d \
  --name owezy-app \
  -p 8080:8080 \
  -e ConnectionStrings__OwezyDb="Server=sqlserver;Database=OwezyDb;User Id=sa;Password=YourStrongPassword123!;TrustServerCertificate=True;" \
  -e Jwt__SigningKey="production-jwt-secret-key-must-be-32-chars-long" \
  -e OtpHasher__SecretKey="production-otp-secret-key-must-be-32-chars-long" \
  -e RECEIPT_STORAGE_ROOT="/app/receipts" \
  -v owezy-receipts-data:/app/receipts \
  owezy:latest
```

---

## Verification & Health Check

### Health Check URL

```http
GET http://<host>:8080/health
```
**Expected Response:** `200 OK` (`Healthy`)

### End-to-End Verification

1. Navigate to `http://<host>:8080/` in a browser — verify Owezy SPA loads.
2. Request OTP & Verify (`/auth/otp/request`, `/auth/otp/verify`).
3. Create a Bill, add participants, upload receipt image, finalize bill.
4. Access participant link (`/participant-access/<token>`) and mark payment.
