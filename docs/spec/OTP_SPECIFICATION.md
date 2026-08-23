# Owezy OTP Authentication Specification

## 1. Objective
Provide secure phone-based Splitter authentication with rate limiting, attempt tracking, secure hashing, and zero cost during local development and testing.

## 2. Phone Normalization
All incoming phone numbers must be sanitized and normalized to E.164 format (e.g. `+919876543210`) using `libphonenumber-csharp` or equivalent regex prior to processing.

## 3. OTP Rules & Constraints
- **Length**: 6 numeric digits (e.g., `584920`).
- **Validity Window**: 5 minutes (300 seconds).
- **Maximum Attempt Limit**: 3 invalid verification attempts per OTP session. Exceeding 3 attempts invalidates the OTP session.
- **Resend Cooldown**: 60 seconds between resend requests for the same phone number.
- **Storage**: Store SHA-256 hash of OTP code (`OtpHash`), `Phone`, `ExpiresAt`, `AttemptCount`, `IsUsed` in `OtpSessions` table. Plaintext OTPs must never be persisted.

## 4. Environment Providers
- **`DevSmsProvider`**: Active in Development/Test environment. Logs generated OTP to standard application logs. Accepts fixed OTP `123456` in automated integration tests when configured.
- **`ProdSmsProvider`**: Active in Production environment. Invokes external SMS gateway API.

## 5. Generic API Security Responses
To prevent phone enumeration attacks, `/api/auth/request-otp` MUST return HTTP 200 OK with a generic message regardless of whether the phone number is already registered or new.
