# Owezy OCR Cost Control & Caching Specification

## 1. Goal
Minimize cloud API execution costs associated with Azure Document Intelligence OCR by establishing strict caching, deduplication, rate-limiting, and error handling policies.

## 2. Processing Pipeline

```text
Image Upload -> Compute SHA-256 Hash -> Check Cache in SQL -> Cache Hit?
   ├── YES ──> Return Cached OCR Result DTO
   └── NO  ──> Check Rate Limit -> Call Azure OCR -> Store Result in Cache -> Return Result DTO
```

## 3. Image Deduplication Specification
- Algorithm: SHA-256 binary digest.
- Storage: `OcrCacheEntries` table in SQL Server storing `ImageHash` (nvarchar(64) PRIMARY KEY), `OcrResultJson` (nvarchar(max)), `CreatedAt`, `LastAccessedAt`.
- Expiration: Cache entries retained permanently or cleaned up after 90 days of inactivity.

## 4. Resilience Policies (Polly Integration)
- **Retry Policy**: Up to 3 retries with exponential backoff (2s, 4s, 8s) for transient HTTP 5xx errors from Azure OCR.
- **Circuit Breaker**: Open circuit for 30 seconds if 50% of requests fail within a 1-minute window.
- **Timeout**: Maximum 15 seconds per Azure OCR call.

## 5. Rate Limiting Rules
- Maximum 5 OCR scan uploads per authenticated Splitter per hour.
- Enforced server-side via ASP.NET Core Rate Limiting Middleware.
