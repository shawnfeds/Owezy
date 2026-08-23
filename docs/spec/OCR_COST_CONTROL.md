# Owezy OCR Cost Control & Caching Specification

## 1. Goal
Minimize cloud API execution costs associated with external OCR services by establishing strict caching, deduplication, rate-limiting, and error handling policies.

## 2. Processing Pipeline

```text
Image Upload -> Compute SHA-256 Hash -> Check Cache in SQL -> Cache Hit?
   ├── YES ──> Return Cached OCR Result DTO
   └── NO  ──> Check Rate Limit -> Call External OCR -> Store Result in Cache -> Return Result DTO
```

## 3. Image Deduplication Specification
- Algorithm: SHA-256 binary digest.
- Storage: `OcrCacheEntries` table in SQL Server storing `ImageHash` (nvarchar(64) PRIMARY KEY), `OcrResultJson` (nvarchar(max)), `CreatedAt`, `LastAccessedAt`.
- Expiration: Cache entries retained permanently or cleaned up after 90 days of inactivity.

## 4. Architectural Resilience Requirements
- **Retry Handling**: Appropriate retry policy with exponential backoff for transient HTTP errors from external OCR API.
- **Circuit Breaker**: Circuit breaking where justified to prevent cascading failures during third-party outages.
- **Timeout Protection**: Controlled external request execution time limits.
- **Implementation Selection**: Concrete resilience libraries/patterns selected during Phase 3 implementation.

## 5. Rate Limiting Rules
- Maximum 5 OCR scan uploads per authenticated Splitter per hour.
- Enforced server-side via ASP.NET Core Rate Limiting Middleware.
