# Asnan — Medical Appointment Application: Architecture

Status: living document. Updated as decisions are made or revisited.

## 1. System Overview

```text
┌─────────────────┐        HTTPS/REST         ┌──────────────────────┐
│   Flutter App    │ ─────────────────────────▶│  ASP.NET Core Web API │
│ (Android / iOS)  │◀───────────────────────── │  (asnan.api)          │
└─────────────────┘        WSS (SignalR)       └──────────┬────────────┘
                                                            │
                                    ┌───────────────────────┼───────────────────────┐
                                    ▼                       ▼                       ▼
                              ┌───────────┐          ┌────────────┐         ┌──────────────┐
                              │  MySQL    │          │  Redis     │         │ External      │
                              │ (EF Core) │          │ (holds,    │         │ Providers      │
                              │           │          │ token      │         │ (Payment, SMS, │
                              │           │          │ blacklist) │         │ Email, Push)   │
                              └───────────┘          └────────────┘         └──────────────┘
```

- **Client**: single Flutter codebase, Android + iOS.
- **Backend**: single ASP.NET Core Web API. No premature microservices — a modular monolith with clear internal boundaries (Auth, Doctors, Scheduling, Appointments, Payments, Chat, Notifications) is the right size for this stage. Boundaries are enforced by folder/namespace structure so services can be extracted later if needed.
- **Realtime**: SignalR hub for chat, served from the same API process.
- **Cache/coordination**: Redis is introduced specifically for slot-hold locking and refresh-token/session bookkeeping at scale. Until Redis is provisioned in an environment, the same behavior is implemented with DB-backed rows + transactions (`AppointmentHolds` table), so local/dev works without extra infra. Redis is an optimization, not a hard dependency, in v1.
- **External providers**: all accessed through interfaces (`IOtpSender`, `IPaymentProvider`, `IPushNotificationSender`, `ICalendarLinkBuilder`) with mock/dev implementations until real credentials exist (see §11).

## 2. Backend Architecture (ASP.NET Core)

### 2.1 Layering

```text
Asnan.Api            → controllers, middleware, DI composition, SignalR hubs
Asnan.Application     → use-case services, DTOs, validators, interfaces for infra
Asnan.Domain          → entities, value objects, enums, domain logic (state machines)
Asnan.Infrastructure  → EF Core DbContext/migrations, repositories, provider adapters
```

Dependency direction: `Api → Application → Domain`, `Infrastructure → Application/Domain`. Domain has zero dependencies on EF Core or ASP.NET types.

**Decision: plain layered architecture, not full CQRS/MediatR.** MediatR pipelines add indirection that isn't earning its keep at this scope (one team, one API). Application services are explicit classes with explicit methods (`AppointmentBookingService.CreateHoldAsync(...)`). This keeps stack traces and navigation simple. Revisit if the codebase grows past the point where a handful of engineers can hold the service layer in their heads.

**Validation**: FluentValidation, one validator per request DTO, run via an action filter so controllers stay thin.

**Errors**: global exception-handling middleware maps exceptions → `ProblemDetails` (RFC 7807) with a consistent envelope: `{ traceId, status, title, detail, errors[] }`. Domain-level errors (e.g. `SlotNoLongerAvailableException`) map to specific status codes (409, 422); unhandled exceptions map to 500 with no internal detail leaked.

**Logging**: Serilog, structured, sinks to console (JSON) in containers. Every request gets a correlation/trace ID (from `Activity`/`TraceIdentifier`) included in every log line and returned in `X-Trace-Id` response header. Redaction rules (see §9) enforced by never logging request/response bodies for auth, payment, OTP endpoints wholesale — only whitelisted fields.

**API versioning**: URL-segment versioning (`/api/v1/...`) via `Asp.Versioning.Mvc`. v1 for the entire initial build.

**Docs**: Swagger/OpenAPI (Swashbuckle), JWT bearer auth wired into the Swagger UI, generated from XML doc comments + FluentValidation-derived schema where practical.

**Health checks**: `/health/live` (process up) and `/health/ready` (DB + Redis + external providers reachable), using `Microsoft.Extensions.Diagnostics.HealthChecks`.

### 2.2 Multi-tenancy / roles

Single MySQL schema. Role-based authorization: `Patient`, `Doctor`, `Admin` (extensible via `Roles`/`UserRoles`). Doctors are users with the `Doctor` role plus a `DoctorProfile`; this keeps auth uniform instead of a parallel doctor-auth system. Object-level authorization is enforced per-request (e.g. a patient can only fetch their own appointments; a doctor only their own schedule) via resource-owner checks in the application layer, not just role checks.

## 3. Flutter Architecture

**Decision: Feature-first Clean Architecture with Riverpod.**

- **Feature-first**, not layer-first at the top level, because features (auth, doctors, appointments, chat) are the unit of change and the unit most contributors reason about; a `screens/`, `widgets/`, `models/` split at the root scales badly past a handful of screens.
- **Riverpod over BLoC**: comparable testability and unidirectional-data-flow guarantees, but less boilerplate per feature, compile-time-safe provider graph, and first-class support for async/family providers which map cleanly onto "doctor availability for date X" style queries. BLoC is not wrong here — this is a judgment call, not a claim that BLoC is deficient — Riverpod is chosen for velocity at this team size.
- **Clean Architecture per feature**: `presentation/` (widgets, controllers/notifiers), `domain/` (entities, use cases, repository interfaces), `data/` (DTOs, repository implementations, remote/local data sources). Cross-feature shared code lives in `core/`.

```text
lib/
  core/
    network/         (dio client, interceptors, error mapping)
    storage/          (secure storage wrappers)
    router/           (go_router config, deep link handling)
    theme/
    widgets/          (shared design-system widgets)
    utils/
  features/
    auth/
    onboarding/
    home/
    doctors/
    doctor_details/
    availability/
    appointments/
    payments/
    chat/
    notifications/
    profile/
    calendar/
```

**State management**: Riverpod (`flutter_riverpod` + code-gen via `riverpod_generator`).
**Navigation**: `go_router`, with typed routes and deep-link support (appointment/chat targets from push notifications).
**Networking**: `dio` with interceptors for auth header injection, automatic access-token refresh on 401 (single-flight refresh to avoid stampede), and structured error mapping to a domain `Failure` type.
**Secure storage**: `flutter_secure_storage` for refresh token and any sensitive cache (Keychain/Keystore-backed); access token can be held in memory only (not persisted) since it's short-lived and reacquired via refresh on cold start.
**DI**: Riverpod providers themselves serve as the DI mechanism — no separate service locator.

Every screen with remote data models its state as `AsyncValue`-driven (loading / data / error), and every feature defines explicit empty-state widgets — this is enforced as a PR-review checklist item (§ "UX States" in the issue template), not a single shared "state enum" abstraction, since empty/error copy is feature-specific.

## 4. Authentication & Session Model

### 4.1 Signup

1. Client submits mobile **or** email.
2. Server issues OTP (§5), rate-limited.
3. Client verifies OTP → server issues a short-lived **signup token** (not a full session) scoped only to "set password / complete profile".
4. Client sets password (min 8 chars + zxcvbn-style strength check server-side, not just character-class rules — see rationale below) → account activated → normal login issued (access + refresh token pair).

**Password policy decision**: NIST 800-63B guidance is followed over legacy composition rules: minimum length 8 (recommend 10+ in UI), maximum length ≥ 64, reject known-breached/common passwords (checked against a local top-10k list, not a live external API, to avoid sending password data anywhere), no forced periodic rotation, no mandatory "must contain 1 uppercase + 1 number + 1 symbol" rule (these rules empirically push users toward predictable patterns like `Password1!`). A strength meter is shown client-side; server rejects passwords below a strength threshold. This is documented so it isn't re-litigated per-issue.

### 4.2 Login

`POST /api/v1/auth/login` with (email|mobile) + password → access token (JWT, 15 min expiry) + refresh token (opaque random 256-bit value, returned once, stored **hashed** server-side, sent to client for secure storage).

### 4.3 Refresh-token model — interpreting "reset session on app open"

The naive reading ("issue a fresh long-lived token every time the app opens, forever") is exactly what §"Token/session behavior" warns against — it creates a session that never truly expires as long as the user opens the app periodically, which is a standing compromise risk once a refresh token leaks.

**Chosen design: rotating refresh tokens with sliding + absolute expiry, per device/session.**

- Each refresh token is single-use. On every use (including "app open" silent refresh) the server issues a **new** access token *and* a **new** refresh token, and revokes the old refresh token — this is the "rotation."
- Each refresh token carries: `familyId` (constant across a device's rotation chain), `deviceId`, `issuedAt`, `expiresAt` (sliding — e.g. 30 days from last use), and the row is deleted/marked used on rotation.
- **Sliding expiry** satisfies "reset on app open": as long as the user opens the app at least once within the sliding window, the session continues indefinitely. This is the safe interpretation of the requirement — rotation happens, but nothing is issued as literally-infinite.
- **Absolute expiry** (e.g. 90 days from first login) is enforced independent of sliding renewals, forcing full re-authentication periodically regardless of usage — bounds the blast radius of a token that's quietly being replayed by an attacker in parallel with the legitimate user.
- **Reuse detection**: if a refresh token that has already been rotated (used and superseded) is presented again, the entire `familyId` is revoked immediately and the user is forced to re-login on all devices tied to that family — this is the standard signal of token theft (attacker and victim both holding the same stolen token, racing to use it).
- **Device/session tracking**: `UserSessions` table records `deviceId`, `deviceName`, `lastSeenAt`, `familyId`, `revokedAt`. Powers "logout this device" / "logout all devices" / a future "manage sessions" screen.
- **Logout**: revokes the specific family (current device) or all families for the user (logout everywhere).
- **Access token validation**: standard JWT signature + expiry check, stateless — no DB hit per request. Revocation of a *session* only affects the next refresh, not already-issued access tokens (this bounds "instant revocation" to at most the access-token lifetime, 15 min — an explicit, documented tradeoff between statelessness/performance and instant revocation. If a future requirement needs instant access-token revocation, add a short-TTL denylist in Redis keyed by `jti`.)

### 4.5 Client behavior

On cold start, the app attempts a silent refresh using the stored refresh token before showing any authenticated screen; failure routes to login. `dio` interceptor performs the same on any 401 mid-session, single-flighted so concurrent requests don't each trigger a rotation race.

## 5. OTP System

Reusable, channel-agnostic:

```csharp
interface IOtpSender { Task SendAsync(string destination, string code, OtpChannel channel); }
interface IOtpService {
  Task<OtpRequestResult> RequestAsync(string destination, OtpPurpose purpose);
  Task<OtpVerifyResult> VerifyAsync(string destination, string code, OtpPurpose purpose);
}
```

- `OtpPurpose`: `SignupVerification`, `Login2fa` (future), `PasswordReset`.
- Codes: 6 digits, hashed at rest (never store plaintext OTP), expire in 5 minutes.
- Max 5 verification attempts per issued code, then the code is invalidated and a new request is required.
- Resend cooldown: 60 seconds; max 5 requests per destination per hour (rate-limited, sliding window).
- One-time use: verified OTP is immediately marked consumed; cannot be replayed.
- Verification failures return a generic "invalid or expired code" — never reveal whether the destination exists (avoids account enumeration) or which specific check failed.
- Providers are swappable via DI/config: `MockOtpSender` (writes to logs in dev only — never in prod config) now; `TwilioSmsSender`, `SendGridEmailSender` (or equivalents) added when credentials exist, behind the same `IOtpSender` interface, selected by `OtpProvider:Sms`/`OtpProvider:Email` config keys.

## 6. Doctor Availability & Scheduling

Backend is the sole source of truth for availability — the client never computes slots.

**Model**:
- `DoctorSchedules`: recurring weekly template (day-of-week, start/end time, slot duration, buffer) per doctor, in the **doctor's own timezone**.
- `DoctorAvailabilityExceptions`: date-specific overrides — holidays/time off (unavailable) or extra availability (exceptional hours).
- Slots are **computed on read**, not pre-materialized for all future time — `GET /availability/doctors/{id}?date=` expands the template + exceptions for that date, then subtracts existing non-cancelled appointments and active holds for that date. This avoids maintaining a huge `AppointmentSlots` table that must be kept in sync, at the cost of a bit more computation per request (acceptable — it's a bounded, cacheable calculation).

**Booking / concurrency** (the critical section of the whole system):

```text
1. Client requests hold:      POST /appointments/holds { doctorId, slotStart }
2. Server, in a DB transaction:
     - re-validates the slot is still free (re-derive availability, not trust client)
     - inserts an AppointmentHold row with a UNIQUE constraint on (doctorId, slotStart)
       WHERE status IN ('Active') — a concurrent second insert for the same slot
       fails at the DB constraint level, not just in application logic
     - hold has a short TTL (e.g. 5 minutes) and a HoldToken returned to the client
3. Client starts payment using the HoldToken as an idempotency key
4. Payment provider confirms (webhook, §7) → server, in a transaction:
     - re-validates hold is still Active and unexpired
     - creates the Appointment row (status Scheduled), marks Hold Consumed
     - this insert is also protected by a unique constraint on (doctorId, slotStart)
       among non-cancelled appointments, as defence in depth beyond the hold
5. Expired, unpaid holds are released by a background sweep (or lazily, on next
   availability read, since the UNIQUE constraint's WHERE clause excludes expired holds)
```

The **DB unique constraint is the actual source of truth for "no double booking"** — the hold/TTL logic is there for good UX (fast feedback, "someone else just took this slot"), not as the sole correctness mechanism. This is the answer to "prevent two users from booking the same slot": application-level checks are necessary but not sufficient under concurrent requests; the constraint is what makes it impossible at the database level.

Payment idempotency: the `HoldToken` (or a generated `PaymentIntentId`) is used as the idempotency key end-to-end so a duplicated webhook delivery or a client retry cannot create two payment records or two appointments for the same hold.

## 7. Appointment Lifecycle

```text
Held → PaymentPending → Scheduled → Completed
  │         │              │
  │         ├─▶ Expired    ├─▶ CancelledByPatient / CancelledByDoctor / CancelledByAdmin
  │         │              │        │
  │         └─▶ PaymentFailed        └─▶ RefundPending → Refunded
  └─▶ Expired                    Scheduled ─▶ NoShow
```

- `Held`: hold row exists, not yet an `Appointment` entity — this keeps unpaid/never-completed attempts out of the appointments table entirely (they live and expire in `AppointmentHolds`).
- `PaymentPending`: appointment intent exists, payment initiated, awaiting provider confirmation.
- `Scheduled`: payment verified server-side; this is the first state that counts as a real, confirmed booking, enables chat, calendar add, reminders.
- `Completed`: derived automatically once `slotEnd` has passed for a `Scheduled` appointment with no cancellation (background job or computed on read).
- `NoShow`: manually/administratively settable distinct from `Completed` (doctor-side action) — kept separate from `Cancelled*` because it has different refund implications.
- `CancelledBy{Patient,Doctor,Admin}`: distinguished because refund policy and notification copy differ by initiator.
- `RefundPending` / `Refunded`: only reachable from a cancelled *paid* appointment.
- All transitions are enforced by a single `AppointmentStateMachine` in `Asnan.Domain`, not scattered `if` checks — every transition is a named, testable method, and invalid transitions throw a domain exception mapped to 409.
- Every transition is recorded in `AppointmentStatusHistory` (who, when, from, to, reason) for audit.

## 8. Payment

```text
Hold created ──▶ Payment initiated ──▶ Provider redirect/SDK ──▶ Provider webhook
                                                                        │
                                                          verify signature, idempotency key
                                                                        │
                                                         Appointment → Scheduled, PaymentTransaction → Succeeded
```

`IPaymentProvider` abstraction:

```csharp
interface IPaymentProvider {
  Task<PaymentSession> CreateSessionAsync(PaymentRequest request);   // amount, currency, idempotencyKey, metadata
  Task<PaymentVerificationResult> VerifyWebhookAsync(HttpRequest req); // signature check + payload parse
  Task<RefundResult> RefundAsync(string providerTransactionId, decimal amount);
}
```

- v1 ships a `MockPaymentProvider` (simulates success/failure, exposes a local "confirm" endpoint gated to dev/staging) behind the interface, so booking end-to-end is fully testable without real payment credentials. A real provider (Stripe is the leading candidate given first-class .NET SDK + webhook signature verification support) is added later behind the same interface — a GitHub issue tracks the credential/config requirement (§11).
- Client-reported "payment succeeded" is **never** trusted to change appointment state — only a verified webhook (or, for the mock provider, a signed equivalent) does that. The client polls/subscribes for the appointment's resulting status rather than asserting it.
- Webhook endpoint verifies provider signature, is idempotent on `providerEventId` (a `ProcessedWebhookEvents` table with a unique constraint dedupes retried deliveries), and runs inside a transaction alongside the appointment-state transition.
- `PaymentTransactions` stores provider reference, amount, currency, status, raw provider event id (not full raw payload with card data — no PAN/PCI data ever touches this system; the provider's hosted checkout/SDK handles card entry).

## 9. Chat

**Decision: SignalR**, not REST polling — chat is inherently bidirectional/low-latency and SignalR gives connection lifecycle, auth, and group semantics for free, appropriate for production (vs. polling, which is simpler but wasteful and laggy).

- Hub connection authenticated via JWT (access token passed as `access_token` query param on the SignalR handshake, standard pattern for browser/mobile clients that can't set headers on the WS upgrade).
- One `ChatConversation` per `Appointment` (created automatically the moment the appointment becomes `Scheduled` — this is the enforcement point for "no chat before a scheduled appointment"). Group name derived from `conversationId`; only the two `ChatParticipants` (the specific patient and specific doctor on that appointment) are permitted to join — authorization checked in `OnConnectedAsync`/per-invocation against the appointment's participant rows, not just "any doctor"/"any patient" role check.
- Messages persisted to `ChatMessages` before broadcast (durable-first, then push) so history/pagination is always consistent with what was delivered.
- Read state: `MessageReadStatus` (per participant, last-read message id/timestamp) — powers unread counts.
- Offline/reconnection: client buffers unsent messages locally and retries on reconnect; server is the source of truth for history (`GET /chat/conversations/{id}/messages?before=`, cursor-paginated). If the recipient isn't connected to the hub, a push notification is sent instead (§10) with the message preview omitted per §"Push Notifications" (no sensitive content in notification text — generic "New message from Dr. X").

## 10. Push Notifications

- **Provider**: Firebase Cloud Messaging (FCM) for both Android and iOS (APNs via FCM), single integration point client-side.
- `NotificationDevices`: `userId`, `fcmToken`, `platform`, `lastSeenAt` — registered on login/app-start, removed on logout/token invalidation, deduped by token.
- `INotificationSender` abstraction so FCM can be swapped/mocked in tests.
- `NotificationPreferences`: per-user, per-category opt-out (e.g. marketing/reminders vs. transactional — transactional notifications tied to security/payment are not user-disable-able).
- Deep links: notification payload carries a typed `deepLink` (e.g. `asnan://appointments/{id}`) consumed by `go_router`'s deep-link handling.
- No medical detail in notification body — appointment notifications say "Your appointment with Dr. X is confirmed", not diagnosis/visit-reason text (none of which this system collects per §"User Profile" data-minimization stance anyway).

## 11. Calendar Integration

`device_calendar` (Flutter plugin) wraps EventKit (iOS) / CalendarContract (Android) behind a single `CalendarService` in `core/`. "Add to Calendar" button on the appointment-detail screen creates a local device event (title, doctor name, start/end from appointment duration, clinic address as location, minimal note) — this is client-side only, no calendar-provider API integration/OAuth needed for v1.

## 12. Database Design

MySQL 8, InnoDB, `utf8mb4`. All timestamps stored as UTC `DATETIME`/`TIMESTAMP`; doctor timezone stored explicitly (`DoctorProfiles.timeZoneId`, IANA id e.g. `Asia/Karachi`) and applied at the point of slot generation/display — never inferred from server locale.

Core tables for the v1 slice (grows per-milestone, not created all at once per the "do not blindly create every table" instruction):

```text
Users, Roles, UserRoles, PatientProfiles, DoctorProfiles, Specialties,
DoctorSpecialties, DoctorSchedules, DoctorAvailabilityExceptions,
AppointmentHolds, Appointments, AppointmentStatusHistory,
PaymentTransactions, Refunds, ProcessedWebhookEvents,
Otps, RefreshTokens, UserSessions,
ChatConversations, ChatParticipants, ChatMessages, MessageReadStatus,
NotificationDevices, Notifications, NotificationPreferences,
AuditLogs
```

Key constraints:
- `RefreshTokens.tokenHash` unique; `familyId` indexed.
- `AppointmentHolds`: unique index on `(doctorId, slotStartUtc)` filtered to `status = 'Active'`.
- `Appointments`: unique index on `(doctorId, slotStartUtc)` filtered to non-cancelled statuses.
- `ProcessedWebhookEvents.providerEventId` unique — idempotency for webhook replays.
- Soft delete (`deletedAtUtc`) on `Users`/`DoctorProfiles` for auditability and to preserve FK history on appointments/payments after account deletion requests; hard delete only via an explicit data-retention job for data with no such requirement.
- `AuditLogs` records security-relevant events (login, password change, refund issued, admin actions) — append-only.

Full ERD is added as `docs/erd.md` (Mermaid) once the Milestone-1 schema is implemented in code — kept next to the migrations so it can't drift silently.

## 13. Security Summary

HTTPS-only (HSTS in prod), JWT signature + expiry validated per request, refresh rotation + reuse detection (§4), passwords hashed with ASP.NET Core's `PasswordHasher` (PBKDF2, salted, versioned) or BCrypt, rate limiting via `Microsoft.AspNetCore.RateLimiting` on `/auth/*` and `/otp/*`, object-level authorization checked in application services (not just `[Authorize(Roles=...)]`), input validation via FluentValidation on every request DTO, EF Core parameterized queries throughout (no raw SQL string concatenation), CORS restricted to known app origins/schemes, standard security headers (`X-Content-Type-Options`, `Referrer-Policy`, etc.) via middleware, no secrets in source (all via environment variables / .NET user-secrets locally, a proper secret manager in prod), webhook signature verification mandatory before trusting any payment callback, chat authorization scoped to the two actual participants per conversation, profile-photo uploads validated by content-type/size/magic-bytes and re-encoded (not served as user-controlled arbitrary files).

Denylist for logs: passwords, OTP codes, access/refresh tokens, payment provider secrets/raw payloads, any future medical free-text fields.

## 14. Deployment Architecture (target, not v1-day-one)

```text
Internet → Nginx (TLS termination, reverse proxy) → ASP.NET Core API (Docker container)
                                                            │
                                                    MySQL (managed or containerized)
                                                    Redis (containerized)
```

Docker Compose for local/staging (`api`, `mysql`, `redis`, `nginx`); migrations run as an explicit step in the deploy pipeline (`dotnet ef database update`), never `EnsureCreated`/auto-migrate-on-boot in production. GitHub Actions CI: build + test on every PR for both `backend/` and `mobile/`; a separate (later-milestone) deploy workflow handles image build/push and remote deploy once a target host/registry is chosen. Kept intentionally minimal at this stage per "do not over-engineer infrastructure initially."

## 15. Open Decisions Requiring Product Input

These are explicitly flagged rather than guessed, per the operating instructions:

1. **Real payment provider** (Stripe vs. a regional/local provider — relevant if this targets a market where Stripe isn't available) — blocks nothing; mock provider unblocks all other work.
2. **Cancellation window & refund percentage schedule** (e.g. free cancellation >24h, 50% refund 6–24h, no refund <6h) — a reasonable default will be implemented as *configurable* values, not hardcoded, so it can be tuned without a code change.
3. **Consultation fee currency/market** — affects payment provider choice and locale formatting.
4. **Whether doctors get their own Flutter app/portal or web-only admin** — v1 assumes doctor-facing actions (schedule management, chat) happen through the same Flutter app with role-based UI, backend APIs are role-agnostic either way.

Everything else in this document represents a reasonable engineering decision made to keep momentum, documented here specifically so it's reviewable/reversible rather than silently assumed.
