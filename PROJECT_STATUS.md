# Transportation Platform — Full System Status & Checklist

> Snapshot date: 2026-06-03 · Baseline: `main` after the 13-phase build-out
> (PRs #18 indexes · #19 Google auth · #20 staff/drivers · #21 PayPal/refunds/cancel ·
> #22 notifications/real-time · #23 reports/demand · #32 observability · #35 counter booking +
> staff refunds · #36 vendor/admin CRUD gaps · #37 XLSX/PDF exports · #38 ratings/reviews ·
> #40 promo codes · seat maps + passenger documents + trip waypoints).
> This is the **full-system** checklist (not the MVP slice), mapping the requirement docs
> (`Docs/01–16`, `SYSTEM_MAP.md`, `ARCHITECTURE.md`) to what is built. Legend: ✅ Done · 🟡 Partial · ❌ Not started.

---

## 0. Quick answers

### Google sign-in / sign-up
**Now implemented** (PR #19). "Continue with Google" is on the login + register pages; the backend
uses an Authorization-Code redirect flow that reuses the existing JWT + HttpOnly refresh-cookie
session. Account-linking is secure (links to an existing account only on a verified email).
Email/password remains the default; Google needs configured credentials to enable.

### Is the system ready for web + mobile?
- **Web: the documented platform is essentially complete** — core booking journey + Google auth +
  Staff actor (incl. trip management) + **real payments (PayPal) with refunds & 48h self-cancel** +
  counter booking + full vendor/admin CRUD + in-app/real-time notifications + reports
  (trip/booking/employee/company, CSV/XLSX/PDF) + demand + **ratings/reviews** + **promo codes** +
  **seat maps + passenger documents + trip waypoints** + customer self-service (profile/password) +
  admin customer management + the bus-scheduling business rules + observability. The full
  requirements traceability (Docs/02–14) is now covered end-to-end (backend + SPA), **~95%** of
  scope. Remaining: geo/live-GPS tracking and an immutable audit-log/soft-delete (intentional omissions).
- **Mobile app: still none** (no Flutter/React Native/native project). Post-MVP, consumes the same API.

### Database
17 → **23 tables** (added `driver`, `refund`, `notification`, `review`, `promo_code`, `trip_stop`; new
columns `application_user.StaffType`, `bus.DriverId`, **`bus.SeatsPerRow`**, `booking.PromoCode`,
`booking.DiscountAmount`, **`passenger.DocumentType`/`DocumentNumber`**). Overbooking remains
structurally impossible; idempotency on bookings/payments/refunds; atomic promo redemption (no
over-redemption under concurrency); recipient-scoped notification inbox indexes; functional/partial
trip-search index. Full detail in §4.

---

## 1. Completion snapshot by area

| Area | Status | Notes |
|---|---|---|
| Architecture & layering (Clean Arch, SOLID, layering test) | ✅ | Enforced by `LayeringTests` |
| Auth — email/password, JWT + rotating refresh, reuse detection | ✅ | |
| **Auth — Google sign-in/sign-up** | ✅ | #19; secure account-linking; opt-in via config |
| Email — verification, password reset, booking confirmation | ✅ | SMTP in prod, logging sink in dev |
| Customer booking journey (search→hold→book→pay→ticket) | ✅ | |
| **Payments — real gateway (PayPal) + refunds** | ✅ | #21; Sandbox default for dev/tests; no card data stored |
| **Customer self-cancel (48h) + refund** | ✅ | #21; frees seats, refunds completed payments |
| **Staff actor** (accounts, suspend/reactivate) + **drivers** | ✅ | #20; company-scoped |
| Vendor console (backend) | ✅ | Bus/trip CRUD + lifecycle, staff, drivers, reports/exports, demand, promo, counter booking, seat-map/stops |
| Admin console (backend) | ✅ | Create/list/activate/suspend/edit/delete company, manager, notify, reports |
| **Vendor/admin CRUD gaps** (edit/delete bus & trip, trip status lifecycle, edit/delete company, profile) | ✅ | #36 |
| **Counter booking (staff selling at the desk) + staff refunds** | ✅ | #35; cash = immediately confirmed |
| **In-app notifications + admin→company** | ✅ | #22; inbox + bell + mark-read |
| **Real-time (SignalR)** — notifications + seat-update broadcast | ✅ | #22; live notifications wired end-to-end; frontend seat-count consumption is a small follow-up |
| **Reports & exports (CSV + XLSX + PDF)** — vendor + admin | ✅ | #23 + #37; revenue/occupancy/totals; formula-injection-safe CSV |
| **Demand forecasting** | ✅ | #23; statistical model over 90-day history |
| **Ratings & reviews** | ✅ | #38; 1–5 + comment; public reads expose display name only (no PII) |
| **Promo codes / discounts** | ✅ | #40; percent/fixed; atomic redemption (no over-redemption) |
| **Seat maps + passenger documents + trip waypoints** | ✅ | this PR; bus layout, optional ID docs, ordered stops |
| Security hardening (rate limits, HSTS, CSP, CORS, anti-enumeration, startup guards) | ✅ | |
| **Email-verified-at-login** (optional config gate) | ✅ | `Auth:RequireEmailVerification`; off by default, refuses login until verified when on |
| Seat concurrency safety + idempotency (bookings/payments/refunds) | ✅ | |
| **Bus scheduling rules** — no overlapping trips per bus, seat-count edit cascades to trips, trip revert/re-activate | ✅ | enforced in schedule/update/revert + bus edit |
| **DB indexing** (functional trip search, filtered outbox, notification inbox, report paths) | ✅ | #18, #22; +booking status/createdAt/createdBy + user companyId |
| **Server hardening audit** — login anti-enumeration (timing + no lockout reveal), public-read/export rate-limit tiers, PayPal retry+circuit-breaker, report-query/index tuning, request body-size cap, health-probe rate-limit exemption, dev-log PII scrub (no token/email), promo-preview input validation | ✅ | #50, #51 + this PR |
| **No sensitive data leaked or logged** — public reads expose no PII (reviews show display-name only; seat-map seat numbers only); tokens never in error bodies/logs; exception handler hides internals in prod | ✅ | full per-endpoint scan |
| Tests (65 unit + 62 integration, Testcontainers Postgres) | ✅ | |
| Docker + CI/CD + security scanning (CodeQL/Dependabot/Scorecard/gitleaks/CodeRabbit) + GHCR | ✅ | |
| **Observability — metrics (Prometheus) + tracing (OTLP)** | ✅ | #32; OpenTelemetry + custom business counters |
| Observability — structured logs + health checks | ✅ | Serilog + `/health` + `/health/ready` |
| Frontend — customer surfaces for newest modules (seat-map picker, trip stops, passenger docs, promo entry, cancel, reviews, live seats) | ✅ | SPA wired to the backend |
| Frontend — vendor operations (trip lifecycle/stops/edit/delete, bus seat-layout/edit/delete, staff, drivers+assign, company profile) | ✅ | SPA wired to the backend |
| Frontend — vendor sales/analytics + admin (reports/export, demand, promo mgmt, counter booking, admin notify + system overview) | ✅ | SPA wired to the backend |
| **Per-role reports** — manager trip/booking/employee, admin system + per-company (revenue by currency) | ✅ | SQL aggregation; CSV/XLSX/PDF |
| **Admin customer management** + **currency allow-list** (SYP/USD/EUR, single source) | ✅ | management-only; per-currency reporting (no FX) |
| Cloud hosting / Infrastructure-as-Code | ❌ | Deferred by decision (images run anywhere; pick a target + add IaC/CD) |
| **Mobile app (iOS/Android)** | ❌ | Post-MVP |

---

## 2. Per-actor

### Customer
✅ register · Google sign-in · email verification · login/logout · forgot/reset password · search ·
**seat map (layout + taken seats)** · hold seats · multi-passenger booking · **passenger ID documents
(optional)** · **promo-code preview + redemption** · **pay (PayPal real / Sandbox dev)** · QR ticket ·
my-bookings · **cancel (48h) + refund** · **rate/review a travelled trip** · **in-app notifications
(bell, real-time, delete)** · **view/edit profile (name + phone)** · **change password (authenticated)**.
❌ saved payment methods (by design — external gateway) · mobile app.

### Vendor / Company Manager
✅ login · add/list/**edit/delete** bus (with seat layout) · schedule/list/cancel/**edit/delete** trip ·
**trip lifecycle (start/complete/revert)** · **set trip waypoints** · **company profile view/edit** ·
**staff CRUD (create/list/**search**/edit/suspend/reactivate/delete)** · **drivers (add/list/search, assign to bus)** ·
**counter booking + cancel/refund** · **promo-code management** · **reports (summary, per-trip,
per-booking, per-employee; CSV/XLSX/PDF)** · **demand prediction** · **admin-notifications inbox**.

### Staff (accountant / supervisor / employee)
✅ account provisioned by the manager, can log in (Staff role + company scope), suspend/reactivate ·
**counter booking (sell at the desk) + cancel/refund** · **full trip management** (schedule/edit/cancel/
start/complete/revert/stops + view buses) via the `VendorOrStaff` policy (delete stays manager-only).
❌ driver sub-type is modelled separately (no login).

### Admin
✅ create/list/activate/suspend/**edit/delete** company · create manager · **notify company** ·
**system summary + per-company reports (revenue by currency)** · **customer management
(list/search/suspend/reactivate/delete, guarded)**.

---

## 3. Backend surface (today)
~70 HTTP endpoints across Auth (incl. Google), Admin (companies CRUD + reports + notify), Vendor
(fleet + trips CRUD/lifecycle/stops, staff, drivers, reports/exports, demand, promo codes, counter
booking), Trips (search, **seat-map**, **stops**), Bookings (hold, book, cancel, promo-preview), Payments
(checkout, webhook, dev-simulate), Reviews (create + public reads), Notifications, plus a SignalR hub
`/hubs/realtime` and a Prometheus `/metrics` endpoint. Background workers: outbox publisher, seat-hold
expiry sweeper. 14 EF migrations.

---

## 4. Database design (current)

**23 tables** = 7 ASP.NET Identity + 16 domain. PostgreSQL, client-generated `uuid` keys
(`ValueGeneratedNever`), money `numeric(12,2)`, `timestamptz`.

### 4.1 Identity tables
`AspNetUsers` (+ custom `FullName`, `CompanyId`, **`StaffType`**), `AspNetRoles`, `AspNetUserRoles`,
`AspNetUserClaims`, `AspNetRoleClaims`, **`AspNetUserLogins`** (backs Google external login), `AspNetUserTokens`.

### 4.2 Domain tables
- **company** — Id · Name · Email **UNIQUE** · Phone · Status · audit · index on `Status`.
- **bus** — Id · CompanyId FK · BusNumber · SeatCount · **`SeatsPerRow` (seat-map layout, default 4)** · Type · Model · **`DriverId` FK→driver (SetNull)** · audit · **UNIQUE(CompanyId, BusNumber)**.
- **driver** *(new)* — Id · CompanyId FK · FullName · Phone? · LicenseNumber? · audit · index on `CompanyId`.
- **trip** — Id · CompanyId FK · BusId FK · Origin · Destination · DepartureUtc · ArrivalUtc · SeatCount · Price · Currency · Status · audit · indexes on `CompanyId`, `BusId`, and functional partial `(lower(Origin), lower(Destination), DepartureUtc) WHERE Status='Scheduled'`.
- **trip_stop** *(new)* — Id · TripId FK (Cascade) · Sequence · Name · ArrivalUtc? · DepartureUtc? · **UNIQUE(TripId, Sequence)** (ordered waypoints between origin and destination).
- **booking** — Id · TripId FK · CustomerEmail · Reference **UNIQUE** · Status · TotalAmount · Currency · **`PromoCode`? · `DiscountAmount`** · IdempotencyKey **UNIQUE** · audit · indexes on `CustomerEmail`, `TripId`.
- **passenger** — Id · BookingId FK (Cascade) · FirstName · LastName · SeatNumber · **`DocumentType`? · `DocumentNumber`?** (optional travel ID).
- **seat_hold** — Id · TripId FK · SeatNumber · HeldBy · BookingId? · ExpiresAtUtc · Consumed · audit · **FILTERED UNIQUE(TripId, SeatNumber) WHERE Consumed=false** · index on `ExpiresAtUtc`.
- **seat_assignment** — Id · TripId FK · SeatNumber · BookingId FK (Cascade) · **UNIQUE(TripId, SeatNumber)** (no-overbooking guarantee).
- **payment** — Id · BookingId FK **UNIQUE** · Gateway · GatewayTxnRef · Status (Pending/Completed/Failed/Refunded) · Amount · Currency · IdempotencyKey **UNIQUE** · audit. No card data (PCI SAQ-A).
- **refund** *(new)* — Id · PaymentId FK (Cascade) · BookingId · Amount · Currency · Status (Pending/Completed/Failed) · GatewayRefundRef? · Reason · IdempotencyKey **UNIQUE** · audit · index on `PaymentId`.
- **notification** *(new)* — Id · RecipientUserId · Title · Message · Type · IsRead · ReadAtUtc? · audit · indexes `(RecipientUserId, IsRead)` and `(RecipientUserId, CreatedAtUtc)`.
- **review** *(new)* — Id · BookingId FK **UNIQUE** · TripId · CompanyId · CustomerEmail (private) · DisplayName (first name + last initial) · Rating (1–5) · Comment? · audit · indexes on `TripId`, `CompanyId`.
- **promo_code** *(new)* — Id · CompanyId FK (Cascade) · Code · DiscountType (Percent/Fixed) · DiscountValue · MaxRedemptions? · RedemptionCount · ExpiresAtUtc? · Active · audit · **UNIQUE(CompanyId, Code)**; over-redemption prevented by an atomic conditional UPDATE.
- **refresh_token** — Id · UserId · TokenHash **UNIQUE** · CreatedAtUtc · ExpiresAtUtc · RevokedAtUtc? · ReplacedByHash? · index on `UserId`.
- **outbox_message** — Id · Type · Payload · OccurredAtUtc · ProcessedAtUtc? · Attempts · Error? · **filtered** index `OccurredAtUtc WHERE ProcessedAtUtc IS NULL`.

### 4.3 Still missing for a full production platform
Geo/coordinates + live GPS-tracking state, immutable `audit_log`/soft-delete. These are intentional
scope omissions, not bugs.

---

## 5. DevOps / Infrastructure
✅ Multi-stage non-root Docker (API + web) + healthchecks · `docker-compose` · CI (build/test/lint) ·
CodeQL · Dependabot · OpenSSF Scorecard · gitleaks (hook + push protection + CI) · CodeRabbit ·
images to **GHCR** · production `StartupGuards` (weak secrets / `*` hosts / no-TLS DB / dev payment
provider / missing PayPal·Google·Email config all fail the boot) · **OpenTelemetry metrics + tracing**.
❌ Cloud hosting / IaC (Terraform/K8s) + automated cloud CD — deferred; choose a target and add it.

---

## 6. Suggested next steps (remaining gaps)
1. **Counter booking + staff refunds** (uses the `VendorOrStaff` policy already in place).
2. **Vendor/admin CRUD gaps** — edit/delete bus & trip, trip status lifecycle, company profile/edit/delete.
3. **Frontend dashboards** — reports/demand views, staff & driver management UI, admin→company composer, live seat counts on search (subscribe to the SignalR trip group).
4. **Exports** — XLSX (ClosedXML) + PDF (QuestPDF, Arabic font).
5. **Ratings/reviews**, promo codes, seat maps, passenger documents (schema + flows).
6. **Cloud** — pick a host, add IaC + cloud CD (migration job → rollout → smoke) with OIDC + log/metrics shipping.
7. **Mobile app** (Flutter / React Native).
8. Optional: **enforce email-verified-at-login** (currently optional to keep register→use seamless).
