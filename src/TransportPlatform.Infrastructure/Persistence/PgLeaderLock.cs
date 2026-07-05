using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace TransportPlatform.Infrastructure.Persistence;

/// <summary>
/// Postgres advisory-lock guard so a background worker's tick runs on at most ONE app instance at a
/// time when the API is scaled horizontally. Every <c>BackgroundService</c> otherwise runs on every
/// instance — harmless for idempotent sweeps, but for the outbox it would fire the same domain-event
/// side effect (e.g. a booking-confirmation email) once per instance. Advisory locks are session-
/// scoped and need no schema/migration; the lock is released before the tick returns, and again
/// automatically if the connection drops (so a crashed leader never wedges the queue).
/// </summary>
public static class PgLeaderLock
{
    // Well-known lock keys — arbitrary but stable and unique per worker.
    public const long Outbox = 8410_0001;
    public const long SeatHoldSweep = 8410_0002;
    public const long RefundReconciliation = 8410_0003;
    public const long DataRetention = 8410_0004;

    /// <summary>
    /// Runs <paramref name="work"/> only if this instance can grab advisory lock <paramref name="key"/>;
    /// returns <c>false</c> without running it if another instance currently holds the lock. Keeps the
    /// connection open for the duration so EF operations inside <paramref name="work"/> reuse the same
    /// lock-holding session, then releases the lock.
    /// </summary>
    public static async Task<bool> TryRunAsLeaderAsync(
        ApplicationDbContext db, long key, Func<Task> work, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        var opened = false;
        if (conn.State != ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync(ct);
            opened = true;
        }

        try
        {
            if (!await AdvisoryLockAsync(conn, acquire: true, key, ct))
                return false; // another instance is the leader for this tick

            try
            {
                await work();
                return true;
            }
            finally
            {
                await AdvisoryLockAsync(conn, acquire: false, key, ct);
            }
        }
        finally
        {
            if (opened)
                await db.Database.CloseConnectionAsync();
        }
    }

    // CommandText is assigned only constant string literals (never a variable/interpolation), and the
    // lock key travels as a bound @k parameter — so there is no SQL-injection surface here.
    private static async Task<bool> AdvisoryLockAsync(DbConnection conn, bool acquire, long key, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = acquire ? "SELECT pg_try_advisory_lock(@k)" : "SELECT pg_advisory_unlock(@k)";
        var p = cmd.CreateParameter();
        p.ParameterName = "k";
        p.Value = key;
        cmd.Parameters.Add(p);
        return await cmd.ExecuteScalarAsync(ct) is true;
    }
}
