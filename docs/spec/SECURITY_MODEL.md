# Owezy Security & Privacy Specification

## 1. Dual Credential Framework
Owezy operates two distinct authorization mechanisms:

```text
               ┌────────────────────────┐
               │    Splitter Access     │
               └───────────┬────────────┘
                           │ Authenticated (Phone + OTP)
                           ▼
                  Bearer JWT Token
                           │
                           ▼
         Full Bill Read/Write & History Access
                           │
                           ▼
               ┌────────────────────────┐
               │   Participant Access   │
               └───────────┬────────────┘
                           │ Token Link (/split/{bToken}/{pToken})
                           ▼
              Cryptographic Tokens
                           │
                           ▼
   Scoped Read Access + Limited Payment-Status Mutation
```

## 2. Splitter Authorization
- **Credential**: JWT Bearer Token issued upon successful phone OTP verification.
- **Claims**:
  - `sub`: Splitter ID (GUID)
  - `phone`: Normalized E.164 phone string
  - `exp`: Expiration timestamp (e.g. 7 days)
- **Permissions**: Full CRUD access over bills created by `sub`. Ability to confirm participant payments and modify bill status.

## 3. Participant Authorization & Token Scoping
- **Credential**: Pair of cryptographically random tokens (`billToken`, `participantToken`).
- **Scoping Definition**: Access is strictly scoped to a specific `(Bill, Participant)` relationship. The participant token is NOT a globally meaningful participant identity.
- **Generation Rules**:
  - Generated using `RandomNumberGenerator.GetBytes(32)` (256-bit entropy).
  - Formatted as URL-safe Base64 strings.
  - Independent of database primary keys, phone numbers, or bill IDs.
- **Access Model**: **"Scoped read access + limited payment-status mutation"**
  - Read: Own participant details, claimed items, calculated share total, payment state, splitter UPI VPA.
  - Limited Mutation: Mark own payment status as paid (`Unpaid` -> `MarkedPaid`).
  - Prohibited: Modifying bill, modifying other participants, viewing other participants' financial info, changing other participants' payment states, finalizing bills, managing bills, accessing splitter history.

## 4. Server-Side Scoped Data Protection
To guarantee participant privacy, server controllers MUST execute scoped query handlers:
- Query: `GetParticipantShareQuery(billToken, participantToken)`
- Verification: Validate `billToken` matches bill and `participantToken` matches participant.
- Result Projection (`ParticipantShareDto`):
  - Returns `BillTitle`, `ParticipantName`, `ClaimedItems` (belonging only to this participant), `CalculatedShareTotal`, `PaymentStatus`, `SplitterUpiId`.
  - Excludes: All other participants' names, items claimed by others, other participants' totals, and total bill master breakdown.

## 5. Security Mitigations
- **IDOR Prevention**: No sequential IDs exposed in API endpoints.
- **Token Revocation**: Splitter can regenerate a participant's link if needed, revoking the previous token.
- **Rate Limiting**: Rate limiting applied to endpoint calls (OTP requests, link accesses, uploads).
