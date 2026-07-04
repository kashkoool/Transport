# TPX Travel — Domain Lifecycle, Race-Condition & Missing-Requirements Analysis

_Produced by a 4-agent code-grounded trace (booking+seats · payment+webhook+refund · cancellation ·
missing-requirements), synthesized with the business-logic-design method. Every finding is
`file:line`-backed against `transport-platform/src`. Date: 2026-07._

## Verdict

The **seat-integrity core is genuinely solid** — double-selling a seat is impossible (two DB unique
indexes), idempotency keys prevent double-bookings and double-charges, promo *caps* are atomic, and
cross-tenant access is IDOR-safe. **But the money-movement and cancellation paths have several
confirmed, high-severity gaps** — the worst being that **cancelling a trip takes no refund action for
paid customers at all**. These are correctness bugs, not style issues, and most involve money.

---

## 1. State machines (as implemented)

### Booking (`BookingStatus`: PendingPayment, Confirmed, Cancelled, Expired)
```
            create
              ▼
        PendingPayment ──Confirm() [webhook, guard=Pending]──▶ Confirmed
          │      │                                                 │
          │      └──Cancel() [not if Confirmed]──▶ Cancelled       └──CancelConfirmed()──▶ Cancelled
          └──Expire()──▶ Expired   ⚠ DEAD: no caller ever invokes Expire()
```
- ⚠ **No `Refunded` state** — a refunded booking stays `Cancelled`; refund lives on a separate entity.
- ⚠ **`Expire()` is dead code** — abandoned pending bookings linger as `PendingPayment` forever.
- ⚠ **`Cancel()` guard is asymmetric** — re-cancels `Expired`/`Cancelled` silently; only blocks `Confirmed`.

### Payment (`PaymentStatus`: Pending, Completed, Failed, Refunded)
```
Pending ──MarkCompleted()[idempotent on Completed only]──▶ Completed ──MarkRefunded()──▶ Refunded
   └──MarkFailed()──▶ Failed
```
- ⚠ `MarkCompleted` has **no guard against `Refunded→Completed` / `Failed→Completed`** (unconditional setters).

### Refund (`RefundStatus`: Pending, Completed, Failed)
```
Pending ──MarkCompleted()──▶ Completed
   └──MarkFailed()──▶ Failed   ⚠ MarkFailed() has ZERO call sites (dead). Failed refunds stay Pending.
```

### Trip (Scheduled, InProgress, Completed, Cancelled)
```
Scheduled ──Start()──▶ InProgress ──Complete()──▶ Completed
   └──Cancel() ⚠(no state guard: can 'cancel' a Completed trip)──▶ Cancelled ──Revert()[bus-free]──▶ Scheduled
```

### Seat: `hold (10-min TTL)` → `assignment (permanent)` → `released / expired`
- Active-hold uniqueness: filtered unique index `seat_hold(TripId,SeatNumber) WHERE Consumed=false`.
- Hard no-oversell: unique index `seat_assignment(TripId,SeatNumber)`.

---

## 2. Invariants & enforcement

| Invariant | Enforced by | Status |
|---|---|---|
| A (trip,seat) sold at most once | DB unique `seat_assignment` | ✅ solid |
| A (trip,seat) actively held at most once | DB filtered-unique `seat_hold` | ✅ solid |
| One booking per idempotency key | DB unique `booking.IdempotencyKey` + app pre-check | ✅ solid |
| One payment per booking / checkout idempotent | DB unique `payment.BookingId` + gateway idempotency | ✅ solid |
| Promo never redeemed past cap | atomic conditional `ExecuteUpdateAsync` | ✅ cap solid — ⚠ **not in booking txn** (leaks on rollback) |
| Booking confirmed only via authoritative webhook | only webhook calls `Confirm()`; dev-sim is Dev-only | ✅ |
| Webhook confirm is idempotent | early-return on `Confirmed` | ✅ |
| Refund at most once per booking | unique `refund.IdempotencyKey` | ✅ (per-booking key; latent if multi-payment added) |
| **Captured amount == booking total** | — | ❌ **NOT enforced** (webhook has no amount field) |
| **Confirmed booking always refunded on trip-cancel** | — | ❌ **NOT done** (trip-cancel issues no refund) |
| **Seats released on cancel exactly once** | RemoveRange (idempotent) | 🟡 ⚠ trip-cancel never frees *confirmed* assignments |
| **Failed refund is retried** | (comment refers to reconciliation job) | ❌ **job does not exist** — stuck `Pending` forever |
| Confirm re-checks trip is still bookable | — | ❌ webhook never consults `trip.Status` |
| Booking has optimistic concurrency token | — | ❌ none → ghost-confirm race |

---

## 3. Worst-case & race-condition table (consolidated)

Legend: ✅ handled · 🟡 partial · ❌ gap

| # | Scenario | Current behavior | Verdict | Fix |
|---|---|---|---|---|
| 1 | Two users book the same seat at once | 2nd hold hits filtered-unique → 409; 2nd confirm hits `seat_assignment` unique | ✅ | — |
| 2 | Double-click / duplicate booking | idempotency-key pre-check + unique index returns the winner | ✅ | — (but see #7 promo) |
| 3 | Double-click *pay* / two checkouts | one payment per booking + PayPal request-id idempotency | ✅ | — |
| 4 | Duplicate webhook delivery | early-return on Confirmed; `Confirm`/`MarkCompleted` idempotent | ✅ | — |
| 5 | **Webhook with valid signature but wrong amount** | confirms booking on the "succeeded" flag; amount never checked | ❌ **critical** | add Amount/Currency to webhook; reject if ≠ booking total before Confirm |
| 6 | **Trip cancelled → paid customers** | no refund, no notification, seats not freed — only counted | ❌ **critical** | in trip-cancel txn: `CancelConfirmed()` + create Pending refund + free seats per booking |
| 7 | **Refund fails at gateway (timeout)** | left `Pending`; comment says "reconciliation job" — job doesn't exist | ❌ **critical** | build a `BackgroundService` that retries `Refund.Status==Pending` (gateway call already idempotent) |
| 8 | **Capture succeeds but confirm txn fails** | PayPal captured *before* DB txn; on failure money taken, booking Pending, no comp | ❌ | payments sweeper for `Completed` payment w/ unconfirmed booking → reconcile/auto-refund |
| 9 | **Seat resold after hold expiry, then old webhook confirms** | `Confirm()` INSERT hits `seat_assignment` unique → txn throws → paid, no seat, no refund | ❌ | catch the clash in webhook → compensating auto-refund + cancel (mirror CounterBooking) |
| 10 | **Ghost-confirm: customer cancels Pending while webhook in flight** | no optimistic lock; webhook can confirm the just-cancelled booking | ❌ | add RowVersion/`xmin` to Booking, or re-check `Status==Pending && trip Scheduled` in webhook txn |
| 11 | **Promo redeemed then booking insert fails** | redemption commits separately → count leaks / code exhausts w/o bookings | ❌ | move `ExecuteUpdateAsync` inside the booking transaction |
| 12 | **Manager shrinks bus seats while trip has holds/pending bookings** | only checks *sold* seats; ignores holds+pending; read-then-write TOCTOU | 🟡 | include holds+pending; row-lock the trip / re-validate at confirm |
| 13 | Multi-passenger partial assignment insert fails | all inserts + status flip in one transaction → atomic rollback | ✅ | — (but see #8/#9 for the money side) |
| 14 | Out-of-order webhook (booking row missing) | returns 404 → gateway retries later | 🟡 | persist unmatched verified events for local reconcile |
| 15 | Double-cancel (click twice / cancel+trip-cancel) | status guard + unique refund key blocks 2nd | ✅ | — |
| 16 | Customer cancel at exactly 48h | `< window` → allowed at exactly 48h; UTC-safe | ✅ | (document boundary is inclusive) |
| 17 | **Counter/desk cancel of an already-departed trip** | no time/status gate on the desk path → staff can refund a completed trip | ❌ | reject when trip `Completed/InProgress` or departed |
| 18 | **Over-refund (always 100%)** | refund = full `payment.Amount`; no tiered/partial; no `Status==Refunded` pre-check | ❌ | tiered refund by time-to-departure + guard against already-refunded |
| 19 | **Revert cancelled trip whose bus is now booked** | bus-free check is not transactional, no exclusion constraint (TOCTOU) | 🟡 | wrap in txn; add exclusion constraint on (bus, time-range) |
| 20 | Booking created on a trip that just started/cancelled | checked at hold+create, **not** at confirm | 🟡 | re-check `trip` at confirm (same fix as #10/#20) |

---

## 4. Confirmed bugs, prioritized (fix these)

**🔴 P0 — money / correctness, fix first**
1. **Trip-cancel strands paid customers** (#6) — no refund, no notice, seats locked. *(3 agents agree.)*
2. **No amount check on payment webhook** (#5) — a 1-SYP capture confirms a full booking.
3. **Missing refund reconciliation job** (#7) — failed refunds stuck `Pending` forever. *(3 agents.)*
4. **Seat-assignment collision leaves paid customer with no seat and no refund** (#9).
5. **Capture-before-confirm** money-with-no-booking, no compensation (#8).

**🟠 P1 — integrity**
6. **Ghost-confirm race** — no optimistic lock on Booking (#10).
7. **Promo redemption not in the booking transaction** — leaks redemptions (#11).
8. **Seat-count shrink ignores holds/pending + TOCTOU** (#12).
9. **Counter/desk cancel has no departure/status gate** (#17).
10. **Flat 100% refund / over-refund possible** (#18) — needs tiered refund + already-refunded guard.

**🟡 P2 — hardening**
11. Revert-trip bus double-book TOCTOU (#19) · 12. Confirm doesn't re-check trip (#20) ·
13. `trip.Cancel()` no state guard · 14. `Expire()` dead / abandoned-booking cleanup ·
15. `Cancel()` asymmetric guard · 16. wire up `Refund.MarkFailed()` (Pending=retry, Failed=alert).

---

## 5. Missing functional requirements (to cover all scenarios)

Beyond the bugs above, these capabilities are absent and needed for a complete operation. **P1**
must-have · **P2** should · **P3** nice.

### Top 8 to add first
1. **Tiered / partial refund by time-to-departure** (P1) — replaces the flat 100% refund (revenue leak).
2. **Trip delay / reschedule + notify affected passengers** (P1) — no "retime" transition exists.
3. **Boarding check-in + driver passenger manifest** (P1) — QR ticket exists but nothing consumes it.
4. **Booking modification (change date/trip/seat)** (P1) — avoid forced cancel-and-lose-the-sale.
5. **Refund/dispute oversight + manual retry console** (P1) — admin surface for stuck refunds.
6. **Queryable admin audit log** (P1) — who did what (refunds, cancels, overrides) for disputes/support.
7. **Self-service account deletion / GDPR export** (P1) — legal; only admin-side delete exists today.
8. **No-show handling** (P2) — a `NoShow` state to free seats and close the refund loop.

### Rest, by subsystem
- **Booking:** transfer/rename passenger (P2) · group/per-booking seat cap (P2) · accessibility seats (P3).
- **Payment:** receipts/invoices (P2) · chargeback/dispute webhook events (P2) · multi-currency display + FX source-of-truth (P3) · wallet/store-credit + loyalty (P3).
- **Trip/Seat ops:** defined "what happens to bookings when a trip is edited" (P2) · waitlist/sold-out queue (P3).
- **Notifications:** SMS/push channel (P2) · pre-departure reminder + refund-completed message (P3).
- **Account/Identity:** email-change + re-verify (P2) · phone OTP verification (P2) · saved passengers (P3).
- **Admin/Ops:** driver app/login surface for the manifest (P2) · support impersonation (P2) · company subscription/billing (P2) · GPS live tracking (P3).
- **Search/UX:** round-trip / return booking (P2) · amenities & bus-type filters (P2) · recurring-trip templates (P2) · surface the seat map before selection (P3).

---

## 6. Design still to complete

The *visual* design is decided (Changa/Cairo · Mediterranean light+dark · cinematic hero; backups in
`docs/design-backups/`, codified in the `premium-frontend` skill). The design work that remains is:
- **Bilingual Arabic + English + RTL** across the whole SPA (the single biggest product gap).
- **New screens** for whichever requirements above are adopted — notably: booking-modify, boarding/
  check-in + driver manifest, tiered-refund display, admin refund/dispute + audit-log console,
  trip delay/reschedule, account deletion. Design these as they're scheduled.

---

## 7. Coverage & blind spots (honesty)

- Findings are static-analysis, `file:line`-grounded, and cross-verified across 4 agents; the two
  strongest claims (seat double-sell prevented; trip-cancel doesn't refund) are backed by direct
  reads and quoted indexes.
- **Not** done: executing the integration tests (some concurrency tests exist and may already cover
  parts of §3 — reproduce before/after any fix); tracing every DTO for the four "inferred-absent"
  requirements (group cap, dispute webhook, reminders, seat-map-UX — high-likelihood, not certain);
  verifying refund-retry cadence at runtime.
- Recommended next step: turn each 🔴/🟠 row into a failing integration test (Given/When/Then), then
  fix — so the guarantee is proven, not asserted.
