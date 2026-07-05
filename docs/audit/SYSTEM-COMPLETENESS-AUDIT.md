# TPX Travel — System Completeness Audit

_Produced by a 4-agent parallel audit (backend inventory · frontend inventory · requirements extraction · readiness/gap scan), cross-verified against the `Docs/` spec. Date: 2026-07._

## Headline

**The system is far more complete than expected — the documented functional requirements for every
actor are implemented, and the .NET backend is genuinely production-grade.** The remaining work is
concentrated in **cross-cutting / "last-mile" areas**, led by the one thing the product's identity
depends on and that is currently **entirely absent: Arabic + English (i18n / RTL).**

The system has **4 actors** (per `Docs/02`): **Admin**, **Company Manager**, **Staff/Employee**,
**Customer/User**. A **Driver** is a *sub-type of Staff* (a data record with no login) — not a
separate actor. (`SuperAdmin` exists as a backend role but is only an alias of Admin.)

---

## Requirements coverage per actor

Legend: ✅ implemented (backend + frontend) · 🟡 partial / with a caveat · ❌ missing

### Admin (`admin`) — 12/12 ✅
| # | Requirement | Status |
|---|---|---|
| 1 | Log in / out | ✅ |
| 2 | Create a transport company (issues manager login) | ✅ |
| 3 | View all companies (+ manager names) | ✅ |
| 4 | Suspend a company | ✅ |
| 5 | Reactivate a company | ✅ |
| 6 | Delete a company (cascade staff) | ✅ (blocked if it has buses/trips) |
| 7 | System statistics (users, companies, trips, bookings) | ✅ |
| 8 | Company reports (activity, revenue) | ✅ |
| 9 | Trip reports (occupancy, filters) | ✅ |
| 10 | Company profit reports | ✅ (grouped by currency) |
| 11 | Send notifications to companies | ✅ |
| 12 | View / filter / open notifications | ✅ |

### Company Manager (`manager`) — 14/14 ✅
| # | Requirement | Status |
|---|---|---|
| 1 | Log in / out | ✅ |
| 2 | Add/edit/delete employees | ✅ |
| 3 | Suspend / reactivate employee | ✅ |
| 4 | View / search employees | ✅ |
| 5 | Add / edit / delete buses | ✅ |
| 6 | Assign driver to bus | ✅ |
| 7 | View / search buses | ✅ |
| 8 | Edit trip seats / view seat map | ✅ |
| 9 | View / edit company profile | ✅ |
| 10 | Company status (bookings, profit) | ✅ |
| 11 | Financial/employee/trip/booking reports + PDF/Excel export | ✅ |
| 12 | Predict demand for a trip | ✅ (statistical, non-ML by design) |
| 13 | View / delete admin notifications | ✅ |
| 14 | Manage trips (shared) | ✅ |

### Staff / Employee (`staff`) — 11/11 ✅
| # | Requirement | Status |
|---|---|---|
| 1 | Log in / out | ✅ |
| 2 | Create a counter booking (walk-in) | ✅ |
| 3 | Book multiple passengers in one transaction | ✅ |
| 4 | View counter bookings + details | ✅ |
| 5 | Search bookings | ✅ |
| 6 | Cancel a counter booking (release seats) | ✅ |
| 7 | Process cash refunds (manual) | ✅ |
| 8 | Search refundable bookings / view refunded amounts | 🟡 refund tracking exists on the desk; dedicated "needs-refund" search is light |
| 9 | Search trips | ✅ |
| 10 | View company buses | ✅ |
| 11 | Manage trips (shared) | ✅ |

### Customer / User (`user`) — 11/11 ✅
| # | Requirement | Status |
|---|---|---|
| 1 | Register / log in / out | ✅ (+ Google OAuth, email verification — extras) |
| 2 | Browse today's trips | ✅ |
| 3 | Search & filter trips | ✅ |
| 4 | View available seats / choose count | ✅ (real seat map) |
| 5 | Book online, multiple passengers | ✅ |
| 6 | Confirm booking (review step) | ✅ |
| 7 | Pay via external gateway (hosted page) | ✅ (PayPal live + sandbox; confirmation is webhook-driven) |
| 8 | Cancel within 48h | ✅ |
| 9 | View / edit profile; change password | ✅ |
| 10 | Notification when a booked trip is cancelled | ✅ |
| 11 | View booking via QR code | ✅ (client-rendered) |

### Shared — Trip management (`manager` or `staff`) — 5/5 ✅
Add (validates bus ownership + no schedule conflict) · Edit (restricted once in-progress) · Cancel
(+ refund all non-cancelled bookings) · Revert · View/search with filters, sort, pagination — all ✅.

**Net: every documented functional requirement, for every actor, is implemented.** 🎯

---

## Implemented *beyond* the documented spec (bonuses)

- **Promo / discount codes** (vendor CRUD + customer preview) — not in `Docs`, fully built.
- **Ratings & reviews** — a future-work item, done.
- **Google OAuth** sign-in + **email verification** + password-reset flows.
- **Real-time** seat-availability updates via SignalR.
- Production-grade infra: refresh-token rotation + reuse detection, tiered rate limiting,
  idempotency keys, outbox pattern, DB-enforced no-overbooking, startup secret guards, Serilog +
  OpenTelemetry + Prometheus, health/readiness endpoints, 65 unit + 62 integration tests.

---

## What's left to finish (prioritized)

### 🔴 Ship-blockers / high value
1. **Bilingual Arabic + English (i18n + RTL) — the biggest gap.** The app is English-only, LTR-only:
   no translation library, no `ar/en` message files, `<html lang="en">` with no `dir`, no `[dir=rtl]`
   styles, no Arabic font in PDF reports, English-only emails. This is the product's core promise and
   it's unbuilt end-to-end (UI + PDF + email). *This is where the Changa/Cairo + Mediterranean design
   work plugs in.*
2. **Refund reliability bug** — code promises a "reconciliation job" to retry failed gateway refunds,
   but **no such job exists**, so a refund that fails at the gateway stays `Pending` forever. Money-
   losing. (Add the hosted retry service, or a manual admin retry.)
3. **Webhook amount re-validation** — `ProcessPaymentWebhook` confirms on a signature-valid webhook
   without re-checking captured **amount/currency == booking total**. Add the check (defense-in-depth).
4. **Real deploy + IaC** — `deploy.yml` only builds & pushes images to GHCR (`:latest`). No
   Terraform/K8s/Helm, no migration-gate job, no smoke test, no rollback. **Nothing actually deploys.**

### 🟠 Quality / integrity
5. **Payment/refund/webhook integration tests** — the most money-sensitive subsystem has **zero**.
6. **Arabic font in QuestPDF** reports (Amiri/Cairo/Noto) — else Arabic renders as boxes.
7. **Frontend tests** — ~1 spec today; add component tests + a Playwright E2E for search→pay→ticket.
8. **Localized transactional emails** + alerting when an email send fails silently.
9. **Signed QR ticket** (HMAC the `TPX|ref|tripId` payload) + render it in emailed/PDF tickets, not
   just the live SPA — makes tickets tamper-evident and usable offline.

### 🟡 Hardening / nice-to-have
10. **SMS channel** (booking confirmation / OTP) — no seam exists; or decide to defer.
11. Minor backend hardening: a "migrations-ran" readiness gate before serving; RefreshToken
    optimistic-concurrency token; convert per-endpoint validation to a global endpoint filter.
12. **Trip-details / deep-link page** on the frontend — the chosen trip is passed in memory, so a hard
    refresh on `/book/:tripId` loses it and bounces to search. Add `GET /api/trips/{id}` + a details page.

### Confirmed absent (future-work, likely out of scope for v1)
Subscription/billing for companies · mobile apps (iOS/Android) · real-time GPS bus tracking ·
local-bank/Syrian PSP integration · immutable audit log.

---

## Coverage & blind spots (honesty)

- Findings are first-hand `file:line` across backend + frontend, cross-verified by 4 agents.
- **Not** done: running the app; rendering a PDF (Arabic-tofu is inferred from the *absent* font
  registration, which QuestPDF requires); line-by-line audit of all ~40 endpoints (grep + spot-reads
  used); confirming at runtime that no reconciliation job runs (code references it; scheduler not
  found — treat the refund bug as high-confidence but verify before/after a fix).
- Actor/requirement mapping is against `Docs/02, 03, 06–09, 10, 14` as the source of truth.
