# Owezy Product Scope Specification

## 1. Product Core Purpose
Owezy is a lightweight, single-purpose bill-splitting application designed to make sharing receipt costs fast, accurate, and privacy-preserving.

## 2. In-Scope Features (v1)
- **Splitter Authentication**: Phone number -> OTP -> JWT login.
- **Bill Management**: Manual creation, item addition, line item quantity/price editing, participant management.
- **Advisory OCR**: Receipt scan upload, advisory extraction (`Name`, `Quantity`, `UnitPrice`, `LineTotal`), OCR result review and manual correction.
- **OCR Cost Controls**: SHA-256 image hashing, SQL analysis caching, upload rate limits, retries.
- **Splitting Engine**: Equal division of claimed items among claimers, Largest Remainder Method rounding, exact paisa reconciliation.
- **Participant Access**: Token-based secure link generation (`/split/{billToken}/{participantToken}`) without participant registration.
- **Participant Scoped Privacy**: Server-side authorization restricting participant view strictly to their own split and claimed items.
- **UPI Settlement**: `upi://pay` deep link generation, participant "Mark as Paid" status, Splitter confirmation.
- **Splitter History**: Simple listing of previous bills created by the authenticated Splitter.

## 3. Strict Out-of-Scope Firewall
The following features are strictly prohibited in v1:
- Push/email notifications or SMS reminder blasts
- In-app chat, comments, or social activity feeds
- Analytics dashboards, charts, spending breakdowns, or reports
- Budgeting features or expense categorization
- Recurring bills or scheduled splitting
- Integrated payment processing, bank APIs, card gateways, or auto-verification
- Multi-currency support (INR `₹` only)
- Subscription plans or paid user tiers
- Admin backoffice dashboards
- Recommendation algorithms or AI financial advice
- Redis, Kubernetes, Event Buses, or Microservice architectures
- Native mobile applications (iOS/Android native codebases)
