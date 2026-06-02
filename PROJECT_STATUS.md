# Transportation Platform — Full System Status & Checklist

> Snapshot date: 2026-06-02 · Baseline: `main` after PR #17 · index notes updated for PR #18
> This is the **full-system** checklist (not the MVP slice). It maps everything the requirement
> docs (`Docs/01–16`, `SYSTEM_MAP.md`, `ARCHITECTURE.md`) describe against what is actually built
> in code today. Legend: ✅ Done · 🟡 Partial · ❌ Not started.

---

## 0. Three quick answers to your questions

### Q: "For email sign-in we are using Google, right? And for signup?"
**No. There is no Google / OAuth / social login anywhere — not for sign-in and not for sign-up.**
Authentication is **email + password** via ASP.NET Core Identity. I verified this across the whole
codebase: there is no `AddGoogle`, no `AddOAuth`, no `ExternalLogin`, and the Angular login page has
**only** email + password fields (no "Sign in with Google" button).

What I implemented in the last task was **email-based account features**, which is a different thing:
- a **verification email** sent on signup (click a link to confirm your address),
- **forgot / reset password** by emailed link,
- a **booking-confirmation email** after payment.

So: email is used to *send messages and verify/reset the account*, not as a Google login. If you
actually want "Sign in with Google", that is a **new feature we have not built** — say the word and
I'll scope it.

### Q: "Is the system fully implemented and ready for websites and mobile app?"
**Web: the core booking journey is fully working end-to-end** (search → hold seat → book → pay
(sandbox) → QR ticket), on a strong security/architecture/DevOps foundation. **But the full system
is not complete** — roughly **35–40%** of the documented full scope is built. Big missing pieces:
the **Staff actor**, **real payment gateway + refunds**, **reports/exports**, **demand forecasting**,
**in-app + admin notifications**, **real-time seat updates**, and **cloud hosting**.
**Mobile app: does not exist at all** (no Flutter / React Native / native project in the repo). Mobile
was always documented as *post-MVP / future work*.

### Q: "Is the database fully [designed]?"
The **core booking schema is excellent** (17 tables, overbooking is impossible by DB constraint,
idempotency + refresh-token rotation built in). But it is **MVP-scoped**: it is missing tables the
full system needs — **ratings/reviews, a notifications table, an audit log, a refund/cancellation
record, a real seat map, passenger ID documents, trip waypoints, promo codes, and geo/tracking**.
Full table-by-table detail in §4.

---

## 1. Completion snapshot by area

| Area | Status | Notes |
|---|---|---|
| Architecture & layering (Clean Arch, SOLID, layering test) | ✅ | Enforced by `LayeringTests` |
| Auth — email/password, JWT + rotating refresh, reuse detection | ✅ | No Google/OAuth, no SMS/OTP |
| Email — verification, password reset, booking confirmation | ✅ | SMTP in prod, logging sink in dev |
| Customer booking journey (search→hold→book→pay→ticket) | ✅ | Payment is **sandbox**, not a real gateway |
| Customer self-service (cancel booking, refund, profile edit, ratings) | ❌ | Not built |
| Vendor/Manager console | 🟡 | Add/list buses, add/list/cancel trips only |
| Vendor staff management, reports, demand prediction, counter booking | ❌ | Not built |
| **Staff actor** (accountant/supervisor/employee/driver) | ❌ | Entire role missing |
| Admin console | 🟡 | Create/list/activate/suspend company, create manager |
| Admin reports, edit/delete company, system dashboard, notifications | ❌ | Not built |
| Payments — real external gateway + refunds | ❌ | Only `SandboxPaymentGateway` (real flow shape, fake URLs) |
| Notifications — in-app feed + admin→company messages | ❌ | Only the booking-confirmation email exists |
| Reporting & exports (CSV / XLSX / PDF) | ❌ | Not built |
| Demand forecasting | ❌ | Not built |
| Real-time (SignalR live seats / status) | ❌ | Not built |
| Security hardening (rate limits, HSTS, CSP, CORS, headers, anti-enumeration) | ✅ | Strong |
| Seat concurrency safety + idempotency | ✅ | DB unique + filtered unique indexes |
| Tests (50 unit + 18 integration, Testcontainers Postgres) | ✅ | Covers auth, booking, concurrency, tenancy, email |
| Docker (API + web, multi-stage, non-root, healthchecks) | ✅ | |
| CI/CD (build/test/lint, CodeQL, Dependabot, Scorecard, gitleaks×3, CodeRabbit) | ✅ | Images pushed to GHCR |
| Cloud hosting / Infrastructure-as-Code | ❌ | Deferred — no Terraform/K8s/cloud CD |
| Observability — structured logs + health checks | ✅ | Serilog + `/health` + `/health/ready` |
| Observability — metrics / tracing / error-tracking (Prometheus/OTel/Sentry) | ❌ | Not built |
| **Mobile app (iOS / Android)** | ❌ | Does not exist |

**Headline:** strong, secure foundation + a complete *core* customer flow; large parts of the full
multi-actor system (especially Staff, real payments, reports, notifications, real-time, mobile,
cloud) remain to be built.

---

## 2. Full checklist by actor

### 2.1 Customer (User)
- ✅ Register (email, password, full name)
- ✅ Email verification (link emailed; **not enforced** at login yet)
- ✅ Login / logout (JWT access + HttpOnly rotating refresh cookie)
- ✅ Forgot / reset password (revokes all sessions on reset)
- ✅ Search trips (origin / destination / date)
- ✅ Hold seats (~10-min hold, race-safe)
- ✅ Multi-passenger booking + confirm (idempotent)
- ✅ Pay — **sandbox gateway** (hosted-checkout shape, signed webhook) 🟡 *(not a real provider)*
- ✅ QR ticket + my-bookings history
- ❌ Cancel own booking (48-hour rule) / self-service refund
- ❌ Profile management (edit details, change password while logged in)
- ❌ Ratings & feedback on trips
- ❌ In-app notifications (only the confirmation email exists)
- ❌ Saved payment methods, promo codes
- ❌ Mobile app

### 2.2 Vendor / Company Manager
- ✅ Manager login (admin provisions the manager account)
- ✅ Add bus, list buses (company-scoped)
- ✅ Schedule trip, list trips, cancel trip (company-scoped)
- ❌ Edit / delete bus; assign driver to bus
- ❌ Edit trip; revert cancelled trip; status transitions (in-progress / completed)
- ❌ Company profile view / edit
- ❌ Staff management (add / edit / delete / suspend employees)
- ❌ Counter booking (sell at the office) + counter refunds
- ❌ Reports (financial, trips, bookings, employee)
- ❌ Demand prediction
- ❌ Admin-notifications inbox

### 2.3 Staff (accountant / supervisor / employee / driver)
- ❌ **Entire actor not implemented** — no staff roles, no staff login sub-types, no counter
  booking, no staff-driven refunds, no `Em_` username rule, no driver→bus assignment.

### 2.4 Admin
- ✅ Onboard company (starts `Pending`)
- ✅ List companies (paginated, filter by status)
- ✅ Activate / suspend company
- ✅ Create vendor-manager login for a company
- 🟡 Multi-level admin: `Admin` + `SuperAdmin` roles exist, but no super-admin-only features
- ❌ Edit / delete company
- ❌ View company status & financials
- ❌ System-wide status dashboard
- ❌ Reports (system, per-company, per-trip, profit) + exports
- ❌ Send notifications to companies

---

## 3. Cross-cutting capabilities

| Capability | Status | Evidence / Notes |
|---|---|---|
| JWT access token (in-memory on client) | ✅ | 15-min default |
| Refresh token rotation + reuse detection (family revoke) | ✅ | SHA-256 hashed, never stored raw |
| HttpOnly + Secure + SameSite=Strict refresh cookie | ✅ | |
| RBAC roles | ✅ | Admin, SuperAdmin, VendorManager, Customer (**no Staff roles**) |
| Multi-tenant isolation (company-scoped) | ✅ | Verified by `VendorTenancyTests` |
| Rate limiting (global/auth/sensitive/webhook, config-driven, real-IP behind proxy) | ✅ | |
| Security headers (CSP, HSTS, X-Frame-Options, nosniff, COOP, Referrer-Policy) | ✅ | |
| Anti-enumeration on forgot-password / resend-verification | ✅ | |
| Email transport (SMTP prod / logging dev) + bounded async send timeout | ✅ | |
| SMS / phone OTP | ❌ | |
| Outbox pattern (reliable domain-event side effects) | ✅ | `OutboxPublisher`, at-least-once |
| Seat-hold expiry sweeper | ✅ | Background worker |
| Idempotency keys (booking, payment) | ✅ | Unique DB constraints |
| Health checks (liveness/readiness) | ✅ | `/health`, `/health/ready` |
| Structured logging (Serilog) | ✅ | |
| Metrics / tracing / error tracking | ❌ | No Prometheus / OpenTelemetry / Sentry |

**Backend surface today:** ~26 HTTP endpoints, 28 application handlers (all implemented, no stubs),
2 background workers, 4 EF migrations.

---

## 4. Database design (current)

**17 tables total** = 7 ASP.NET Identity + 10 domain. PostgreSQL, all PKs are `uuid` (client-generated,
`ValueGeneratedNever`), money is `numeric(12,2)`, timestamps are `timestamptz`. 4 migrations.

### 4.1 Identity tables (ASP.NET Core Identity, Guid keys)
`AspNetUsers` (+ custom `FullName`, `CompanyId`), `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`,
`AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserTokens`. Standard Identity columns (Email,
NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, LockoutEnd, AccessFailedCount, …).

### 4.2 Domain tables (columns → type)

**company** — `Id` uuid PK · `Name` varchar(200) · `Email` varchar(256) **UNIQUE** · `Phone` varchar(40)
· `Status` varchar(20) (Pending/Active/Suspended) · `CreatedAtUtc` · `UpdatedAtUtc` · `CreatedBy` ·
index on `Status` (active-companies subquery + admin list-by-status)

**bus** — `Id` PK · `CompanyId` FK→company *(Restrict)* · `BusNumber` varchar(40) · `SeatCount` int ·
`Type` varchar(20) (Standard/Premium/Luxury/Sleeper) · `Model` varchar(100) · audit cols ·
**UNIQUE(CompanyId, BusNumber)**

**trip** — `Id` PK · `CompanyId` FK→company · `BusId` FK→bus · `Origin` varchar(120) ·
`Destination` varchar(120) · `DepartureUtc` · `ArrivalUtc` · `SeatCount` int · `Price` numeric(12,2) ·
`Currency` varchar(3)="SYP" · `Status` (Scheduled/InProgress/Completed/Cancelled) · audit cols ·
indexes on `CompanyId`, `BusId`, and a **functional partial** search index
`(lower("Origin"), lower("Destination"), "DepartureUtc") WHERE "Status"='Scheduled'`

**booking** — `Id` PK · `TripId` FK→trip · `CustomerEmail` varchar(256) · `Reference` varchar(40)
**UNIQUE** · `Status` (PendingPayment/Confirmed/Cancelled/Expired) · `TotalAmount` numeric(12,2) ·
`Currency` · `IdempotencyKey` varchar(100) **UNIQUE** · audit cols · index on `CustomerEmail`, `TripId`

**passenger** — `Id` PK · `BookingId` FK→booking *(Cascade)* · `FirstName` · `LastName` · `SeatNumber` int

**seat_hold** — `Id` PK · `TripId` FK→trip *(Cascade)* · `SeatNumber` int · `HeldBy` varchar(256) ·
`BookingId` uuid (nullable) · `ExpiresAtUtc` · `Consumed` bool · audit cols ·
**FILTERED UNIQUE(TripId, SeatNumber) WHERE Consumed=false** (the live-hold lock) · index on `ExpiresAtUtc`

**seat_assignment** — `Id` PK · `TripId` FK→trip · `SeatNumber` int · `BookingId` FK→booking *(Cascade)* ·
**UNIQUE(TripId, SeatNumber)** — the ultimate "no overbooking ever" guarantee

**payment** — `Id` PK · `BookingId` FK→booking *(Cascade)* **UNIQUE** · `Gateway` varchar(50) ·
`GatewayTxnRef` varchar(200) · `Status` (Pending/Completed/Failed/Refunded) · `Amount` numeric(12,2) ·
`Currency` · `IdempotencyKey` varchar(100) **UNIQUE** · audit cols. **No card data stored** (PCI SAQ-A by design).

**refresh_token** — `Id` PK · `UserId` uuid · `TokenHash` varchar(128) **UNIQUE** · `CreatedAtUtc` ·
`ExpiresAtUtc` · `RevokedAtUtc` · `ReplacedByHash` varchar(128) · index on `UserId`

**outbox_message** — `Id` PK · `Type` varchar(200) · `Payload` text(JSON) · `OccurredAtUtc` ·
`ProcessedAtUtc` · `Attempts` int · `Error` varchar(2000) · **filtered** index on
`OccurredAtUtc WHERE "ProcessedAtUtc" IS NULL` (the publisher's poll predicate)

### 4.3 Is the schema "fully" designed?
**For the core flow — yes, and it's well done.** Overbooking is structurally impossible (two layers of
unique indexes), retries are safe (idempotency keys), refresh tokens rotate with theft detection.

**For the full system — no, these tables are missing:**
- ❌ `review` / `rating` (customer feedback, trip/driver ratings)
- ❌ `notification` (in-app feed + admin→company messages; outbox only holds transient events)
- ❌ `audit_log` (immutable change history / compliance; only `Created/UpdatedAtUtc` exist)
- ❌ `refund` / cancellation record (reason, partial refund, chargeback; `payment.Status` has `Refunded` but no detail table)
- ❌ real **seat map** (bus has only `SeatCount`; no seat types window/aisle/accessible, no layout)
- ❌ passenger **ID/document** fields (only first/last name)
- ❌ trip **waypoints / intermediate stops** (only origin + destination)
- ❌ `promo_code` / discount
- ❌ geo / coordinates / live-tracking state (origin/destination are plain text)
- ❌ `staff` / employee specifics + driver→bus link (the Staff actor isn't modeled)

---

## 5. DevOps / Infrastructure

✅ **Done:** Multi-stage non-root Dockerfiles (API + web) with healthchecks · `docker-compose` (Postgres,
one-off migrator, API, web, Adminer) · CI (build/test/lint) · CodeQL · Dependabot · OpenSSF Scorecard ·
gitleaks (pre-push hook + push protection + CI) · CodeRabbit · images pushed to **GHCR** · production
`StartupGuards` (fails boot on weak secrets / missing email / `*` hosts / no TLS).

❌ **Not done:** Cloud hosting / IaC (no Terraform, CloudFormation, K8s, or Helm) · automated cloud CD ·
metrics/tracing/error-tracking · log shipping to a cloud sink. The images run anywhere Docker runs;
choosing a target (Azure Container Apps / AWS ECS / Fly.io / a VPS) is the next deploy decision.

---

## 6. Suggested roadmap to "full system" (priority order)

1. **Real payment gateway + refunds** — swap `SandboxPaymentGateway` for a regional provider behind the
   existing `IPaymentGateway`; add refund flow + a `refund` table; customer **cancel-booking (48h)**.
2. **Staff actor** — roles + login sub-types, counter booking, staff refunds, driver→bus assignment.
3. **Notifications** — `notification` table + in-app feed + admin→company messages (reuse the outbox).
4. **Reports & exports** — admin/manager financial, trip, booking, employee reports; CSV/XLSX/PDF.
5. **Vendor/admin CRUD gaps** — edit/delete bus & trip, trip status lifecycle, edit/delete company, profile.
6. **Real-time** — SignalR live seat counts + trip status.
7. **Demand forecasting** — the statistical endpoint described in the docs.
8. **Cloud** — pick a host, add IaC + cloud CD (migration job → rollout → smoke test) + log/metrics shipping.
9. **Ratings/feedback**, promo codes, seat maps, passenger documents (schema + flows).
10. **Mobile app** (Flutter or React Native) — post-MVP, consumes the same API.

> Optional but recommended before "Google sign-in" or mobile: decide whether to **enforce email
> verification at login** (currently it's optional so the register→use flow stays seamless).
