# Scalability & Stability Readiness — Transport Platform

_.NET 10 API + Postgres + Angular (prerendered, nginx). Dated 2026-07-05._

Scope: horizontal-scale readiness — background workers under multiple instances, rate limiting,
connection ceiling, statelessness, idempotency, caching. Evidence is `file:line` + live behaviour.

## 1. Live infra snapshot
- **App tier:** single container today (compose / local). Stateless — auth is JWT (no server session),
  seat holds + refresh tokens live in Postgres, no in-memory store on the request path. **Ready to run
  as N instances** behind a load balancer once one is provisioned (see gaps closed below).
- **DB:** Postgres 17 (compose). HA/replica, backups+PITR, slow-query insight = **deploy-time config**
  (not set locally) — track when a managed DB is chosen.
- **Connection ceiling:** was default (100/instance) — now **bounded** (see §3). Postgres default
  `max_connections=100`.
- **Cache/queue:** none (no Redis). Rate limiting + the outbox are in-process.
- **Edge/CDN:** none yet; prerendered static assets are long-cached by nginx.

## 2. Trap scan
| Trap | State | Evidence |
|---|---|---|
| Single instance / no LB | Stateless, LB-ready | JWT auth; no in-memory session (`auth.service.ts`, `HttpCurrentUser.cs`) |
| Local-disk state | Clean | grep for `File.Write`/`/tmp` on request path → none; QuestPDF/ClosedXML stream to the response |
| Hot-path blocking | Clean | Password hashing via Identity (not on a loop); reports/exports are on-demand endpoints, not the booking path |
| In-memory store defeating scale-out | **Rate limiter only** (see §3) | `Program.cs:141` in-memory `PartitionedRateLimiter` |
| Uncoordinated concurrency (crons on every instance) | **FIXED** (see §3) | 4 `AddHostedService` (`Program.cs:180-183`) |
| Unindexed hot queries | Clean | Verified in the DB audit (functional/partial indexes match every hot path) |
| Idempotency / concurrency control | Strong | Unique idempotency keys (booking/refund/payment), atomic `ExecuteUpdate` promo redemption, seat unique indexes |

## 3. Gaps found — and what was done

**(a) Background workers ran on every instance — FIXED (correctness).**
`OutboxPublisher` did an unlocked `SELECT unprocessed → dispatch side-effect → mark processed`
(`OutboxPublisher.cs`), so 2+ instances would **send the same confirmation email twice**. Added a
Postgres **advisory-lock leader guard** (`PgLeaderLock.cs`) — only one instance drains the outbox per
tick; released each tick, auto-released if the leader crashes (no schema/migration). Applied the same
guard to `RefundReconciliationWorker` (external gateway calls). Left `SeatHoldExpirySweeper` and
`DataRetentionWorker` unguarded **on purpose** — they are pure idempotent `ExecuteDelete` sweeps (safe
to run on every instance; a lock would add cost for no correctness gain). Verified: 69 integration
tests (which drive the outbox) pass under the lock.

**(b) Connection ceiling was implicit — FIXED (deliberate).**
Per-instance pool is now explicitly bounded (`DependencyInjection.cs`) via `Database:MaxPoolSize`
(default **20**), so `MaxPoolSize × instances` stays well under `max_connections=100` with headroom for
migrations/admin. The documented next step past this ceiling is a pooler (PgBouncer).

**(c) Rate limiter is per-instance — DOCUMENTED (deliberate, not yet a bug).**
`Program.cs:141` uses the in-memory `PartitionedRateLimiter`; with N instances the effective limit is
~N× the configured value (each instance counts independently). Correct and sufficient at 1–2 instances.
**Move when scaling out:** back the limiter with Redis (distributed counters) or enforce the limit at the
edge/CDN/WAF. Not worth adding a Redis dependency before a multi-instance deploy exists.

## 4. Milestone map (1 → 1M)
| Users | Breaks first | Move |
|---|---|---|
| 1 → 1k | Nothing | Watch metrics + slow queries |
| 1k → 10k | DB CPU; first slow query | Right-size DB; the indexes are already in place |
| 10k → 100k | **DB connections** (`pool × instances` → `max_connections`) | Raise Postgres tier / add **PgBouncer**; add a read replica for search |
| 100k → 1M | Rate-limiter fan-out; outbox throughput; single write node | Redis-backed rate limit; switch outbox to `FOR UPDATE SKIP LOCKED` for parallel drain; partition the biggest tables |

## 5. Top 3 actions before the next 10×
1. **Provision a managed Postgres with backups+PITR+a replica**, and set `Database:MaxPoolSize` for the chosen instance count.
2. **Run the k6 load script on staging** (`tests/load/baseline.js`) to find the latency knee, then set the edge rate-limit just below it.
3. **Add PgBouncer** (transaction pooling) the first time connection count trends toward the ceiling.

## 6. Verdict
**READY for a small multi-instance deploy** (2–4 instances). The correctness blocker (duplicate outbox
side-effects) is fixed; statelessness, idempotency, and indexing are sound; the connection ceiling is now
deliberate. Before a *large* scale-out: distributed rate limiting + a connection pooler + a load-tested
capacity number (all listed above). None are blockers for launch.
