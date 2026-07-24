# TPX Travel — Project Reference

Quick reference for running the app locally, demo logins, the tech stack, architecture
recommendations, and the mobile path. (Generated 2026-07 during the hardening/perf/UI pass.)

---

## 1. Run it locally

```bash
cd transport-platform
POSTGRES_PASSWORD=change_me_local_only docker compose up -d
# Web:  http://localhost:8080     API (proxied): http://localhost:8080/api
```

The API auto-seeds a rich **~6-month demo dataset** on a fresh DB (Development only). To reset the
data: `docker compose down -v && docker compose up -d`.

Currently seeded: **5 companies · ~174 trips (past completed + upcoming) · ~1,170 bookings ·
~2,198 passengers · ~1,154 payments · ~219 reviews · 15 promo codes**.

---

## 2. URLs to check (per actor)

**Customer / public** — `/` (search) · `/login` · `/register` · `/forgot-password` ·
`/my-bookings` · `/account` · then `/book/:tripId` → `/pay/:bookingId` → `/ticket/:bookingId`
**SEO landing pages** — `/routes` · `/bus/damascus-to-aleppo` · `/city/damascus`
**Vendor console** — `/vendor/trips` · `/vendor/buses` · `/vendor/staff` · `/vendor/drivers` ·
`/vendor/promo` · `/vendor/company` · `/vendor/desk` · `/vendor/reports`
**Admin console** — `/admin/companies` · `/admin/users` · `/admin/reports`
**API docs (dev)** — `/scalar/v1`

---

## 3. Demo logins — password `Demo!Passw0rd` for all

| Role | Email |
|---|---|
| Admin | `admin@tpx.local` |
| Vendor (Demo Lines, richest data) | `vendor@tpx.local` |
| Vendor (other companies) | `manager@damascuslines.local`, `manager@aleppoexpress.local`, `manager@coastaltransit.local`, `manager@orientbuses.local` |
| Staff | `accountant@<company-slug>.local`, `supervisor@<company-slug>.local` |
| Customer (populated: 3 past + 1 upcoming booking) | `customer@tpx.local` |
| Customers (pool across the history) | `customer1@tpx.local` … `customer40@tpx.local` |

---

## 4. Tech stack

| Layer | Stack |
|---|---|
| **Backend** | ASP.NET Core **.NET 10**, Clean Architecture (Domain/Application/Infrastructure/Api), Minimal APIs, hand-rolled handlers (no MediatR), FluentValidation, **EF Core 10**, ASP.NET Identity + **JWT**, **SignalR** (realtime), Serilog, QuestPDF/ClosedXML (PDF/XLSX), transactional **outbox** + background workers (advisory-lock leader-elected for multi-instance safety) |
| **Frontend** | **Angular 20** (standalone components, signals, zone CD), **Tailwind v4**, @ng-icons/phosphor, dependency-free bilingual i18n (EN/AR, RTL), **@angular/ssr prerendering (SSG)**, served by nginx (gzip) |
| **Database** | **PostgreSQL 17** + Npgsql, EF migrations, **UUID** primary keys, functional/partial indexes |
| **Payments** | PayPal REST v2 + a Sandbox gateway, HMAC-signed webhooks, idempotency keys |
| **Caching** | In-process **ASP.NET OutputCache** on anonymous read endpoints. **No Redis.** |
| **Infra / CI** | Docker + compose; GitHub Actions → GHCR; Trivy, CodeQL, Semgrep, gitleaks, Scorecard, dependency-review |

---

## 5. Redis? — Not used, and not needed for a single instance

Add Redis only when running **2+ API instances**, for three things (the code has notes where):
distributed **rate-limiting**, a **SignalR backplane** (realtime across instances), and a shared
**OutputCache** store. Optionally a job/queue later.

---

## 6. Architecture choices — REST vs GraphQL vs WebSocket, etc.

| Tech | Use when… | For this system |
|---|---|---|
| **REST** (current) | Resource CRUD, cacheable, one clear contract, mobile-friendly | ✅ Keep — correct default |
| **GraphQL** | Many diverse clients, flexible nested/aggregated queries, over/under-fetch pain | ❌ Not recommended — adds schema/resolver/N+1/caching/auth complexity for a fixed API (HotChocolate if ever needed) |
| **WebSocket / SignalR** (current) | Real-time push: notifications, live seat maps | ✅ Keep for notifications; add a Redis backplane at multi-instance |
| **gRPC** | High-perf internal service-to-service RPC | Only if split into microservices |
| **SSE** | One-way server push (lighter than WS) | SignalR already covers this |
| **Message broker** (Kafka/RabbitMQ/Service Bus) | Cross-service events, high throughput, decoupling | DB **outbox** is right for now; move to a broker at scale |

**Frontend:** Angular 20 is a strong long-term choice; signals suffice for state (NgRx SignalStore only if state grows).
**Database:** PostgreSQL is an excellent long-term pick. Future note: random **UUIDv4** PKs can fragment indexes at very high write volume — **UUIDv7** (time-ordered) is the modern best practice for PK locality; a later optimization, not needed now.
**Infra:** add a host, **OpenTelemetry** export (Serilog + health checks already present), managed-DB **backups/PITR**.

---

## 7. Database + token verification (audited this session)

- **UUID PKs:** Yes (GUID, non-sequential) — blunts enumeration/IDOR.
- **Indexing:** Well-covered — functional partial route-search index `(lower(origin), lower(destination), departure) WHERE Scheduled`, FK indexes, unique constraints for seat/refund/payment concurrency.
- **Access token:** 15-min JWT (HS256), kept in an **in-memory** SPA signal (never localStorage).
- **Refresh token:** **HttpOnly + Secure + SameSite=Strict** cookie, **SHA-256-hashed at rest**, **rotated every refresh**, **reuse-detection revokes the whole family**, 14-day expiry, FK-cascade to the user.
- **Logout:** revokes the refresh token **server-side** + clears the cookie; the SPA clears state even if the server call fails.
- **Suspend / delete:** revoke tokens **immediately** (per-request lockout re-check) + FK-cascade removes refresh rows on delete.

---

## 8. Angular UI components — recommendation

You already have a **cohesive custom Tailwind design system** (`.btn/.card/.input`, phosphor icons,
premium shadows + micro-interactions + animation utilities). Best long-term fit: **keep it and add
[Angular CDK](https://material.angular.dev/cdk)** — the *headless, accessible* primitives (overlay/
dialog, focus-trap, a11y, virtual-scroll, drag-drop, date-adapter) that you style with your own
Tailwind classes. Avoid a full skinned library (Angular Material / PrimeNG) — it would fight your
look. (PrimeNG only if you later want a heavy data-grid for the admin/vendor consoles.)

---

## 9. Cross-platform mobile (iOS + Android)

- **Backend: 100% reusable, zero changes.** The REST API + JWT work identically for native. (The SPA
  keeps the refresh token in a cookie; a native app uses the token the API *already returns in the JSON
  body* and stores it in the device **Keychain/Keystore** — a client storage choice, no backend change.)
- **Angular is a WEB framework** — by itself it is a web app + installable **PWA**, NOT a native app.
- **To ship real App Store / Play Store apps without a frontend rewrite:** wrap the existing Angular app
  with **Capacitor** (native container hosting the web app + native-API plugins). Same codebase →
  installable hybrid apps. Recommended pragmatic path.
- **Fully-native UI/feel:** **Flutter** or **React Native** — a frontend rewrite, but the .NET backend
  is untouched.

---

## 10. What's left for the project

Everything functional/secure/tested/performant is done. The one remaining milestone is **deployment**:
pick a host (Render = easy free path; Fly.io; a cheap VPS; or a cloud), then a `STAGING_URL` secret
activates the Tier 3/4 CI (E2E/ZAP/k6), and `Seo:PublicBaseUrl` + the SPA `siteBaseUrl` get set to the
real origin.
