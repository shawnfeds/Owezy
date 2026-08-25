# Handoff — Receipt Capture & OCR Foundation Complete

## State

Receipt Capture & OCR Foundation milestone is complete. Working tree clean.

## What Was Added

- Domain Layer (`Owezy.Domain.Receipts`):
  - `ReceiptId` strongly-typed ID.
  - `ReceiptStatus` (`Created = 1`, `Processed = 2`, `Failed = 3`) enum.
  - `OcrLineItem` value object (`Description`, `Quantity?`, `UnitPrice?`, `LineTotal?`, `IsLineTotalDerived`, `Confidence?`).
  - `OcrReceiptDraft` value object (`MerchantName?`, `ReceiptDate?`, `Currency?`, `Subtotal?`, `Tax?`, `Discount?`, `Total?`, `LineItems`).
  - `Receipt` aggregate root (`Id`, `BillId`, `StorageKey`, `Status`, `CreatedAt`, `OcrDraft`, `MarkProcessed`, `MarkFailed`).
- Application Layer (`Owezy.Application.Receipts`):
  - `IOcrService` abstraction.
  - `IReceiptStorage` abstraction.
  - `IReceiptRepository` interface.
  - `OcrDraftNormalizer` static normalizer implementing quantity × unit price derivation rules.
  - `ReceiptDtos` (`UploadReceiptResult`, `ReceiptDraftResult`).
  - `IReceiptService` & `ReceiptService` orchestrating validation, storage, OCR, normalisation, and failure recovery.
- Infrastructure Layer (`Owezy.Infrastructure`):
  - `TesseractOcrService` implementing `IOcrService` using local open-source Tesseract 5.2.0.
  - `ReceiptTextParser` heuristic regex-based receipt text parser.
  - `LocalFileReceiptStorage` storing images on filesystem with server-generated GUID keys and path traversal protection.
  - `ReceiptRow` & `ReceiptConfiguration` EF Core configuration storing JSON OCR drafts (no image binary in SQL).
  - `SqlReceiptRepository` implementing `IReceiptRepository`.
  - EF Core migration `AddReceipts`.
- API Layer (`Owezy.Api.Receipts`):
  - `ReceiptDtos` HTTP response records.
  - `ReceiptEndpoints`: `POST /bills/{billId}/receipt` (image upload) and `GET /bills/{billId}/receipt/{receiptId}` (splitter-only).
- Tests:
  - `OcrDraftNormalizerTests.cs` (5 unit tests covering Cases A-D).
  - `ReceiptServiceTests.cs` (10 unit tests with fake OCR/storage).
  - `ReceiptApiTests.cs` (7 integration tests with fake OCR/storage).

## Key Architectural Guarantees

- **Free/Local OCR**: Tesseract open-source engine used; zero paid API dependencies.
- **OCR Isolation**: OCR output produces a draft only; NEVER automatically modifies `Bill`, `BillItems`, `Participants`, or payment status.
- **Storage Security**: Server-generated GUID keys used (`{Guid}.{ext}`); client filenames are never used on disk; path traversal protected.
- **File Validation**: Extension whitelist (`.jpg`, `.jpeg`, `.png`), Content-Type whitelist, max 10MB file size, and magic byte validation (`FF D8 FF`, `89 50 4E 47`).
- **Normaliser Rule**: Existing detected `LineTotal` is authoritative (Case A). If missing, derived from `Quantity` * `UnitPrice` (Case B). If ambiguous, left as null (Case D). Never silently invents numbers.

## Test Results

| Suite | Pass | Total |
|---|---|---|
| Unit | 161 | 161 |
| Integration/API | 63 | 63 |
| Architecture | 4 | 4 |
| **Total** | **228** | **228** |

## Next

Wait for next explicit instruction.
