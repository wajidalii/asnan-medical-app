# Deployment

This covers the backend API's local/staging Docker Compose stack, every
environment variable it reads, the migration step, and the CD pipeline. See
[ARCHITECTURE.md §14](ARCHITECTURE.md) for the target deployment shape this
implements. The Flutter mobile app is distributed through app stores, not
covered here.

## Local/staging stack (Docker Compose)

```text
Internet → nginx (TLS termination) → api (ASP.NET Core, container) → mysql
                                                                    → redis (reserved, unused today)
```

### Quick start

```bash
cp .env.example .env               # fill in real values — see the table below
./deploy/nginx/generate-dev-cert.sh  # self-signed cert for local HTTPS
docker compose run --rm migrator   # explicit migration step — see below
docker compose up -d
curl -k https://localhost:8443/health/live
```

`api` and `nginx` are internal to the compose network — `nginx` is the only
service with published ports (`8443` → HTTPS, `8080` → HTTP, redirects to
HTTPS). The self-signed cert from `generate-dev-cert.sh` is for local/staging
only; a real deployment terminates TLS with a certificate from a real CA
(e.g. via a managed load balancer, or `certbot` on the nginx host) instead of
that script.

### Migrations are never automatic

ARCHITECTURE.md §14 is explicit: migrations run as an **explicit** pipeline
step, never `EnsureCreated`/auto-migrate-on-boot. The `api` container has no
`dotnet ef` tooling and no code path that calls `Database.Migrate()` — by
construction, not just convention. The compose file's `migrator` service
(profile `tools`, so `docker compose up` never starts it on its own) is the
only thing that runs `dotnet ef database update`:

```bash
docker compose run --rm migrator
```

Run this once before the first `docker compose up`, and again after pulling
any change that adds a new migration — same command, in the CD pipeline, as
an explicit step before the new `api` image is deployed (see below).

### Redis

Included per ARCHITECTURE.md §14's target diagram, provisioned for future
use (a SignalR backplane or a distributed rate-limit store once the API runs
as more than one instance). Nothing in the app talks to it yet — chat
presence tracking is in-memory (`InMemoryChatPresenceTracker`) and rate
limiting is per-instance, both fine for a single API replica.

## Environment variables

Every variable below maps to an ASP.NET Core configuration key via the
standard `__` (double-underscore) convention — e.g. `Jwt__SigningKey` sets
`appsettings.json`'s `Jwt:SigningKey`. None of these have real values
committed anywhere in this repo; `.env.example` documents them with
placeholders only, and `appsettings.Development.json` has separate
dev-only values that are never used outside `ASPNETCORE_ENVIRONMENT=Development`.

| Variable | Config key | Required | Description |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | — | Yes | `Development` or `Production`. Gates Swagger UI, HSTS, and whether a `Mock` provider is even allowed to start (see below). |
| `MYSQL_ROOT_PASSWORD` | — (mysql container only) | Yes | MySQL root password, used only by the `mysql` container's own bootstrap. |
| `MYSQL_DATABASE` / `MYSQL_USER` / `MYSQL_PASSWORD` | — (mysql container only) | Yes | The app's own database/user/password, provisioned by the `mysql` container on first boot. |
| `ConnectionStrings__Default` | `ConnectionStrings:Default` | Yes | Full MySQL connection string the API itself uses. In compose this is built from the `MYSQL_*` values above (see `docker-compose.yml`) — set directly for a non-compose deploy. |
| `Jwt__Issuer` / `Jwt__Audience` | `Jwt:Issuer` / `Jwt:Audience` | Yes | JWT issuer/audience claims. Any stable string; must match between the API and anything else that validates its tokens. |
| `Jwt__SigningKey` | `Jwt:SigningKey` | Yes | Symmetric signing key, 32+ random characters. Rotating this invalidates every issued access/refresh token. |
| `Otp__HashingKey` | `Otp:HashingKey` | Yes | HMAC key used to hash stored OTP codes — the code itself is never stored or logged in cleartext outside Development's mock senders. |
| `OtpProvider__Email` / `OtpProvider__Sms` | `OtpProvider:Email` / `OtpProvider:Sms` | Yes | `Mock` or a real provider name. **`Mock` is refused outside `Development`** (`RequireDevelopmentIfMock` in `DependencyInjection.cs` throws at startup) — only `Mock` exists today; see issue #41 for real-provider credential tracking. |
| `Cors__AllowedOrigins__0` (and `__1`, `__2`, ... for more) | `Cors:AllowedOrigins` | Only if serving a browser client | Origins allowed to call the API cross-origin (the Flutter *web* target, or a future admin web app). Native mobile clients aren't subject to CORS and need nothing here. |
| `Payment__Provider` | `Payment:Provider` | Yes | `Mock` or a real provider name. Same `Development`-only restriction as the OTP providers above; see issue #41/#60. |
| `Payment__MockWebhookSigningKey` | `Payment:MockWebhookSigningKey` | Only if `Payment__Provider=Mock` | Signing key the mock provider uses for its own webhook deliveries. |
| `Notification__Provider` | `Notification:Provider` | No | Empty/unset uses the no-op sender (logs instead of pushing — always safe, not Development-gated). Set to `Fcm` once real Firebase credentials exist (issue #71). |
| `Notification__Fcm__CredentialsJson` | `Notification:Fcm:CredentialsJson` | Only if `Notification__Provider=Fcm` | The full Firebase service-account JSON, as a single-line string. |

Variables not listed above (`Otp__CodeLength`, `Hold__TtlMinutes`,
`CancellationPolicy__RefundTiers`, `Reminders__OffsetsMinutes`,
`PhotoStorage__*`, etc.) have working defaults in `appsettings.json` and only
need overriding if a deployment wants different behavior — see that file for
the full list.

### Why `Mock` providers refuse to start outside Development

Only mock implementations of the OTP and payment providers exist today
(`MockEmailOtpSender`, `MockSmsOtpSender`, `MockPaymentProvider`) — see issue
#41 for tracking what real credentials are needed to replace them. Rather
than silently running a real deployment on fake providers, the app fails
fast at startup (`RequireDevelopmentIfMock`) if `ASPNETCORE_ENVIRONMENT` is
anything but `Development` and no real provider is configured. This is why
the compose file's `ASPNETCORE_ENVIRONMENT` default is `Development` — it's
a local/staging convenience default, not a production one. A real deploy
overrides it to `Production` and must configure real providers first.

## CD pipeline

`.github/workflows/backend-cd.yml` builds and pushes the `api` image to
GitHub Container Registry on every push to `main` that touches `backend/`.
It does not deploy anywhere yet — the actual remote deploy target (host,
orchestrator, managed MySQL, etc.) is an open decision (ARCHITECTURE.md §15)
and is intentionally left as a follow-up issue rather than blocking this one
on a choice that hasn't been made. Once a target is chosen, the deploy job
should run, in order:

1. Pull/build the new `api` image (already done by this workflow).
2. `docker compose run --rm migrator` (or the target's equivalent) against
   the target database — the explicit migration step, always before step 3.
3. Roll out the new `api` image.

## Manual verification performed for this PR

`docker compose build migrator api`, then `docker compose run --rm
migrator` against a fresh `mysql` container (created all 27 tables +
`__EFMigrationsHistory`), then `docker compose up -d api nginx`, then:

- `curl -k https://localhost:8443/health/live` → `Healthy` (200)
- `curl -k https://localhost:8443/health/ready` → `Healthy` (200, proves the
  `api` container can reach `mysql` over the compose network)
- `curl -k https://localhost:8443/api/v1/ping` → `{"status":"ok"}`
- `curl http://localhost:8080/...` → 301 redirect to `https://`
- Response headers through the full nginx → api path include
  `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`,
  `Permissions-Policy` (issue #36's security-headers middleware, confirmed
  working end-to-end through the reverse proxy).
