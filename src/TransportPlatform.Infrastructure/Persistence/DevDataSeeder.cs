using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Fleet;
using TransportPlatform.Domain.Identity;
using TransportPlatform.Domain.Payments;
using TransportPlatform.Domain.Promotions;
using TransportPlatform.Domain.Reviews;
using TransportPlatform.Domain.Trips;
using TransportPlatform.Infrastructure.Email;
using TransportPlatform.Infrastructure.Identity;

namespace TransportPlatform.Infrastructure.Persistence;

/// <summary>
/// Seeds demo data so a fresh Development database is usable end-to-end immediately AND looks
/// like a real, lived-in platform: ~6 months of trip/booking history across several vendors, so
/// every actor's screens (customer bookings, vendor trips/reports, admin dashboard) show
/// meaningful numbers instead of a handful of rows. Idempotent (bails once richly seeded — company
/// count > 1) and wired to run ONLY in Development — it must never execute against a real database.
/// Uses a FIXED-SEED <see cref="Random"/> for every distribution choice (which route, which status,
/// how many seats sold, ...) so a fresh reseed is reproducible; entity ids still come from
/// <see cref="Guid.NewGuid()"/> as normal.
/// </summary>
public sealed class DevDataSeeder(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    IClock clock,
    IReferenceGenerator references,
    ILogger<DevDataSeeder> logger)
{
    // Demo credentials — Development only. Documented in the README so you can log straight in.
    private const string Password = "Demo!Passw0rd";
    public const string AdminEmail = "admin@tpx.local";
    public const string VendorEmail = "vendor@tpx.local";
    public const string CustomerEmail = "customer@tpx.local";

    // ── Tuning knobs (kept together so the seeded volume is easy to reason about / retune) ──
    private const int RandomSeed = 12345;
    private const int CustomerPoolSize = 40;
    private const double TripCancelProbability = 0.06;
    private const double NoShowRollCeiling = 0.05;
    private const double CustomerCancelRollCeilingPast = 0.13;
    private const double CustomerCancelRollCeilingFuture = 0.08;
    private const double AbandonedBookingProbability = 0.08;
    private const double ReviewProbability = 0.30;
    private const int PastOccupancyMin = 8, PastOccupancyMax = 51;
    private const int FutureOccupancyMin = 5, FutureOccupancyMax = 36;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        try
        {
            await SeedCoreAsync(ct);
        }
        // CodeQL: intentional catch-all — dev seeder must never break app startup.
        catch (Exception ex)
        {
            // Never let a seeding hiccup crash startup — the API is still useful without demo data.
            LogSeedFailed(logger, ex);
        }
    }

    private async Task SeedCoreAsync(CancellationToken ct)
    {
        // "Looks empty of demo volume": a fresh DB (0 companies), or a DB that only ever got the
        // OLD minimal seed (exactly 1 company) still gets the rich upgrade. Once richly seeded
        // (5 companies), this bails — the seeder never duplicates data on restart.
        if (await db.Companies.CountAsync(ct) > 1)
            return;

        await EnsureRolesAsync();

        var now = clock.UtcNow;
        var rng = new Random(RandomSeed);

        await EnsureUserAsync(AdminEmail, "Demo Admin", UserRoles.Admin, companyId: null);

        var customerPool = await BuildCustomerPoolAsync(rng);
        var quota = new DemoCustomerQuota();
        var totals = new SeedTotals();

        foreach (var spec in BuildCompanySpecs())
            await SeedCompanyAsync(spec, customerPool, quota, rng, now, totals, ct);

        LogSeeded(logger, totals.Companies, totals.Trips, totals.Bookings, totals.Passengers, totals.Payments, totals.Reviews, null);
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var role in UserRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole<Guid>(role));
        }
    }

    private async Task EnsureUserAsync(string email, string fullName, string role, Guid? companyId, StaffType? staffType = null)
    {
        if (await users.FindByEmailAsync(email) is not null)
            return;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true, // demo accounts skip verification (not implemented yet)
            FullName = fullName,
            CompanyId = companyId,
            StaffType = staffType,
        };
        var result = await users.CreateAsync(user, Password);
        if (!result.Succeeded)
        {
            LogUserFailed(logger, EmailMask.Mask(email), string.Join("; ", result.Errors.Select(e => e.Code)), null);
            return;
        }

        var roleResult = await users.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
            LogUserFailed(logger, EmailMask.Mask(email), string.Join("; ", roleResult.Errors.Select(e => e.Code)), null);
    }

    /// <summary>Ensures the demo customer + a pool of generated customer1..N accounts exist, and
    /// returns the generated pool's emails (the demo customer is handled separately via a
    /// guaranteed-booking quota, so it is intentionally NOT in this pool).</summary>
    private async Task<List<string>> BuildCustomerPoolAsync(Random rng)
    {
        await EnsureUserAsync(CustomerEmail, "Demo Customer", UserRoles.Customer, companyId: null);

        var emails = new List<string>(CustomerPoolSize);
        for (var i = 1; i <= CustomerPoolSize; i++)
        {
            var email = $"customer{i}@tpx.local";
            var (first, last) = PickName(rng);
            await EnsureUserAsync(email, $"{first} {last}", UserRoles.Customer, companyId: null);
            emails.Add(email);
        }
        return emails;
    }

    private async Task<Company> GetOrCreateCompanyAsync(string name, string email, string phone, CancellationToken ct)
    {
        var existing = await db.Companies.FirstOrDefaultAsync(c => c.Email == email, ct);
        if (existing is not null)
            return existing;

        var company = new Company(name, email, phone);
        db.Companies.Add(company);
        return company;
    }

    private async Task SeedCompanyAsync(
        CompanySpec spec, List<string> customerPool, DemoCustomerQuota quota,
        Random rng, DateTimeOffset now, SeedTotals totals, CancellationToken ct)
    {
        var slug = Slugify(spec.Name);
        var company = await GetOrCreateCompanyAsync(spec.Name, $"ops@{slug}.local", spec.Phone, ct);
        company.Activate();
        totals.Companies++;

        var buses = new List<Bus>();
        var busCount = rng.Next(3, 6); // 3-5
        for (var i = 0; i < busCount; i++)
            buses.Add(new Bus(company.Id, $"{spec.Prefix}-{i + 1}", rng.Next(40, 53), PickBusType(rng)));
        db.Buses.AddRange(buses);
        totals.Buses += buses.Count;

        var drivers = new List<Driver>();
        var driverCount = rng.Next(2, 4); // 2-3
        for (var i = 0; i < driverCount; i++)
        {
            var (first, last) = PickName(rng);
            drivers.Add(new Driver(company.Id, $"{first} {last}",
                $"+9639{rng.Next(1_000_000, 10_000_000)}", $"DRV-{rng.Next(1000, 10000)}"));
        }
        db.Drivers.AddRange(drivers);
        totals.Drivers += drivers.Count;

        for (var i = 0; i < Math.Min(buses.Count, drivers.Count); i++)
            buses[i].AssignDriver(drivers[i].Id);

        await EnsureUserAsync(spec.ManagerEmail, spec.ManagerName, UserRoles.VendorManager, company.Id);

        var staffCount = rng.Next(1, 3); // 1-2
        await EnsureUserAsync($"accountant@{slug}.local", $"{spec.Name} Accountant", UserRoles.Staff, company.Id, StaffType.Accountant);
        totals.Staff++;
        if (staffCount >= 2)
        {
            await EnsureUserAsync($"supervisor@{slug}.local", $"{spec.Name} Supervisor", UserRoles.Staff, company.Id, StaffType.Supervisor);
            totals.Staff++;
        }

        db.PromoCodes.AddRange(
            new PromoCode(company.Id, "WELCOME10", DiscountType.Percent, 10m, 200, now.AddMonths(4)),
            new PromoCode(company.Id, "FLAT5000", DiscountType.Fixed, 5000m, 100, now.AddMonths(2)),
            new PromoCode(company.Id, "SUMMER23", DiscountType.Percent, 15m, 300, now.AddDays(-20))); // already expired
        totals.PromoCodes += 3;

        await db.SaveChangesAsync(ct); // batch: company + fleet + staff/manager + promo codes

        var routes = PickRoutesForCompany(rng, rng.Next(2, 4)); // 2-3 routes, each usable in both directions
        var trips = new List<Trip>();
        var pastTripCount = rng.Next(20, 31); // 20-30
        var futureTripCount = rng.Next(6, 11); // 6-10
        for (var i = 0; i < pastTripCount; i++)
            trips.Add(CreateTrip(company, buses[i % buses.Count], routes[rng.Next(routes.Count)], now, rng.Next(-183, 0), rng));
        for (var i = 0; i < futureTripCount; i++)
            trips.Add(CreateTrip(company, buses[i % buses.Count], routes[rng.Next(routes.Count)], now, rng.Next(1, 15), rng));

        db.Trips.AddRange(trips);
        totals.Trips += trips.Count;

        await db.SaveChangesAsync(ct); // batch: trips

        var ctx = new CompanySeedContext();
        foreach (var trip in trips)
            SeedTripBookings(trip, customerPool, quota, rng, now, ctx, totals);

        await db.SaveChangesAsync(ct); // batch: bookings + passengers + seat assignments + payments

        ApplyCompanyOutcomes(ctx, rng, totals);
        await db.SaveChangesAsync(ct); // batch: cancellations/refunds/no-shows + trip start/complete/cancel

        GenerateReviews(ctx, company, rng, totals);
        await db.SaveChangesAsync(ct); // batch: reviews
    }

    /// <summary>Builds one trip on the given day offset from <paramref name="now"/> (negative =
    /// past, positive = future) at a randomly-picked departure hour. Mirrors the original seeder's
    /// <c>now.AddDays(x).Date.AddHours(h)</c> idiom for computing a UTC-midnight-anchored time.</summary>
    private static Trip CreateTrip(Company company, Bus bus, RoutePair route, DateTimeOffset now, int dayOffset, Random rng)
    {
        var forward = rng.Next(2) == 0;
        var origin = forward ? route.Origin : route.Destination;
        var destination = forward ? route.Destination : route.Origin;

        var hour = DepartureHours[rng.Next(DepartureHours.Length)];
        var departure = now.AddDays(dayOffset).Date.AddHours(hour);
        var departureUtc = new DateTimeOffset(departure, TimeSpan.Zero);
        var arrivalUtc = departureUtc.AddHours(route.DurationHours);

        var price = ComputePrice(route.BasePrice, bus.Type, rng);
        return new Trip(company.Id, bus.Id, origin, destination, departureUtc, arrivalUtc, bus.SeatCount, new Money(price, "SYP"));
    }

    private void SeedTripBookings(
        Trip trip, List<string> customerPool, DemoCustomerQuota quota,
        Random rng, DateTimeOffset now, CompanySeedContext ctx, SeedTotals totals)
    {
        var isPast = trip.DepartureUtc < now;
        var seats = new HashSet<int>();

        if (isPast && rng.NextDouble() < TripCancelProbability)
        {
            ctx.ToCancelTrips.Add(trip);
            var cancelledBookingCount = rng.Next(1, 5);
            for (var i = 0; i < cancelledBookingCount && seats.Count < trip.SeatCount; i++)
            {
                var passengers = Math.Min(trip.SeatCount - seats.Count, rng.Next(1, 4));
                if (passengers <= 0)
                    break;
                var email = customerPool[rng.Next(customerPool.Count)];
                var (booking, payment) = CreateConfirmedPaidBooking(trip, email, passengers, seats, rng, totals);
                ctx.ToCancel.Add(new PendingCancellation(booking, payment, 1.0m, "Trip cancelled by operator"));
            }
            return;
        }

        if (isPast)
            ctx.ToComplete.Add(trip);

        if (isPast && quota.PastRemaining > 0)
        {
            var (booking, _) = CreateConfirmedPaidBooking(trip, CustomerEmail, rng.Next(1, 3), seats, rng, totals);
            ctx.ReviewCandidates.Add(new ReviewCandidate(booking, trip, Force: true));
            quota.PastRemaining--;
        }
        else if (!isPast && quota.FutureRemaining > 0)
        {
            CreateConfirmedPaidBooking(trip, CustomerEmail, rng.Next(1, 3), seats, rng, totals);
            quota.FutureRemaining--;
        }

        var occupancyPct = isPast ? rng.Next(PastOccupancyMin, PastOccupancyMax) : rng.Next(FutureOccupancyMin, FutureOccupancyMax);
        var targetSeats = Math.Min(trip.SeatCount, (int)Math.Round(trip.SeatCount * occupancyPct / 100.0, MidpointRounding.AwayFromZero));

        while (seats.Count < targetSeats)
        {
            var passengers = Math.Min(targetSeats - seats.Count, rng.Next(1, 4));
            if (passengers <= 0)
                break;
            var email = customerPool[rng.Next(customerPool.Count)];
            var (booking, payment) = CreateConfirmedPaidBooking(trip, email, passengers, seats, rng, totals);

            var roll = rng.NextDouble();
            if (isPast)
            {
                if (roll < NoShowRollCeiling)
                    ctx.ToMarkNoShow.Add(booking);
                else if (roll < CustomerCancelRollCeilingPast)
                    ctx.ToCancel.Add(new PendingCancellation(booking, payment, SyntheticRefundFraction(rng), "Customer cancellation"));
                else
                    ctx.ReviewCandidates.Add(new ReviewCandidate(booking, trip, Force: false));
            }
            else if (roll < CustomerCancelRollCeilingFuture)
            {
                var fraction = RefundPolicy.RefundFraction(trip.DepartureUtc - now);
                ctx.ToCancel.Add(new PendingCancellation(booking, payment, fraction, "Customer cancellation"));
            }
        }

        if (rng.NextDouble() < AbandonedBookingProbability)
            CreateAbandonedBooking(trip, customerPool[rng.Next(customerPool.Count)], rng, totals);
    }

    /// <summary>Creates a fully Confirmed + paid booking (mirrors the counter-sale flow: create,
    /// confirm — which allocates the permanent seat assignments — then record a completed
    /// payment). Callers decide afterwards (in <see cref="ApplyCompanyOutcomes"/>) whether it
    /// stays Confirmed, becomes a no-show, or gets cancelled + refunded.</summary>
    private (Booking Booking, Payment Payment) CreateConfirmedPaidBooking(
        Trip trip, string customerEmail, int passengerCount, HashSet<int> usedSeats, Random rng, SeedTotals totals)
    {
        var seatNumbers = PickFreeSeats(rng, trip.SeatCount, usedSeats, passengerCount);
        var passengers = seatNumbers.Select(seat =>
        {
            var (first, last) = PickName(rng);
            return new Passenger(first, last, seat);
        }).ToList();

        var booking = Booking.Create(trip.Id, customerEmail, references.NewBookingReference(),
            passengers, trip.Fare, $"seed-{Guid.NewGuid():N}");
        booking.Confirm();

        var payment = new Payment(booking.Id, "Sandbox", booking.Total, $"seed-pay-{booking.Id:N}");
        payment.MarkCompleted($"SBX-{Guid.NewGuid():N}");

        db.Bookings.Add(booking);
        db.Payments.Add(payment);

        totals.Bookings++;
        totals.Passengers += passengers.Count;
        totals.Payments++;

        return (booking, payment);
    }

    /// <summary>An abandoned checkout: created but never paid, then cancelled — never touches a
    /// seat assignment (mirrors a lapsed seat hold that was never confirmed).</summary>
    private void CreateAbandonedBooking(Trip trip, string customerEmail, Random rng, SeedTotals totals)
    {
        var (first, last) = PickName(rng);
        var passenger = new Passenger(first, last, rng.Next(1, trip.SeatCount + 1));
        var booking = Booking.Create(trip.Id, customerEmail, references.NewBookingReference(),
            [passenger], trip.Fare, $"seed-{Guid.NewGuid():N}");
        booking.Cancel("Customer cancellation");

        db.Bookings.Add(booking);
        totals.Bookings++;
        totals.Passengers++;
    }

    /// <summary>Phase B: settles every trip/booking outcome decided while generating bookings.
    /// Runs AFTER the Phase-A save so seat assignments are tracked entities that can be freed
    /// exactly like the real cancel handlers do (query-free here since this context never lost
    /// track of them).</summary>
    private void ApplyCompanyOutcomes(CompanySeedContext ctx, Random rng, SeedTotals totals)
    {
        foreach (var trip in ctx.ToCancelTrips)
            trip.Cancel();

        foreach (var trip in ctx.ToComplete)
        {
            trip.Start(trip.DepartureUtc.AddMinutes(rng.Next(-10, 21)));
            trip.Complete(trip.ArrivalUtc.AddMinutes(rng.Next(-10, 21)));
        }

        foreach (var pending in ctx.ToCancel)
        {
            db.SeatAssignments.RemoveRange(pending.Booking.SeatAssignments);
            pending.Booking.CancelConfirmed(pending.Reason);
            totals.CancelledBookings++;

            if (pending.RefundFraction <= 0m)
                continue; // under-24h-style tier: seat freed, no money returned — matches RefundPolicy

            var refundAmount = Math.Round(pending.Payment.Amount * pending.RefundFraction, 2, MidpointRounding.ToEven);
            if (refundAmount <= 0m)
                continue;

            var refund = new Refund(pending.Payment.Id, pending.Booking.Id,
                new Money(refundAmount, pending.Payment.Currency), pending.Reason, $"refund-{pending.Booking.Id:N}");
            refund.MarkCompleted($"SBX-REF-{Guid.NewGuid():N}");
            pending.Payment.MarkRefunded();
            db.Refunds.Add(refund);
            totals.Refunds++;
        }

        foreach (var booking in ctx.ToMarkNoShow)
        {
            booking.MarkNoShow();
            totals.NoShowBookings++;
        }
    }

    /// <summary>Phase C: a subset of confirmed bookings on completed trips leave a review (every
    /// FORCEd candidate — the demo customer's guaranteed past bookings — always does).</summary>
    private void GenerateReviews(CompanySeedContext ctx, Company company, Random rng, SeedTotals totals)
    {
        foreach (var candidate in ctx.ReviewCandidates)
        {
            if (!candidate.Force && rng.NextDouble() > ReviewProbability)
                continue;

            var passenger = candidate.Booking.Passengers.FirstOrDefault();
            var displayName = passenger is null ? "Traveller" : $"{passenger.FirstName} {passenger.LastName[..1]}.";
            var rating = rng.Next(3, 6); // 3..5

            var review = new Review(candidate.Trip.Id, company.Id, candidate.Booking.Id,
                candidate.Booking.CustomerEmail, displayName, rating, ReviewComments[rng.Next(ReviewComments.Length)]);
            db.Reviews.Add(review);
            totals.Reviews++;
        }
    }

    // ── Static, side-effect-free helpers (name/price/route/seat generation) ────────────────

    private static List<CompanySpec> BuildCompanySpecs() =>
    [
        new("Demo Lines", "+963000000", VendorEmail, "Demo Vendor", "DEM"),
        new("Damascus Lines", "+963111000", "manager@damascuslines.local", "Damascus Lines Manager", "DML"),
        new("Aleppo Express", "+963211000", "manager@aleppoexpress.local", "Aleppo Express Manager", "ALX"),
        new("Coastal Transit", "+963411000", "manager@coastaltransit.local", "Coastal Transit Manager", "CST"),
        new("Orient Buses", "+963311000", "manager@orientbuses.local", "Orient Buses Manager", "ORB"),
    ];

    private static readonly RoutePair[] Routes =
    [
        new("Damascus", "Aleppo", 5.0, 35_000m),
        new("Damascus", "Latakia", 4.0, 30_000m),
        new("Damascus", "Homs", 2.0, 15_000m),
        new("Homs", "Hama", 1.0, 8_000m),
        new("Aleppo", "Latakia", 3.5, 25_000m),
        new("Damascus", "Tartus", 3.5, 27_000m),
        new("Damascus", "Deir ez-Zor", 6.0, 40_000m),
    ];

    private static readonly int[] DepartureHours = [6, 8, 10, 12, 14, 16, 18, 20];

    private static readonly string[] FirstNames =
    [
        "Ahmad", "Mohammed", "Omar", "Khaled", "Hassan", "Yusuf", "Karim", "Bilal", "Samer", "Tarek",
        "Rami", "Fadi", "Nabil", "Marwan", "Adel", "Layla", "Sara", "Fatima", "Rania", "Nour",
        "Maya", "Dana", "Huda", "Rana", "Lina", "Salma", "Yara", "Reem", "Amal", "Mona",
    ];

    private static readonly string[] LastNames =
    [
        "Al-Homsi", "Nasser", "Khalil", "Youssef", "Haddad", "Saleh", "Mansour", "Darwish", "Aziz", "Sabbagh",
        "Barakat", "Farhat", "Kassem", "Zaidan", "Chamoun", "Hamdan", "Qassem", "Halabi", "Shaaban", "Najjar",
    ];

    private static readonly string[] ReviewComments =
    [
        "Comfortable ride and on time.",
        "Great service, will book again.",
        "Driver was very professional.",
        "Bus was clean and seats were comfortable.",
        "Smooth trip, no complaints.",
        "A bit delayed but overall a good trip.",
        "Excellent value for the price.",
        "Friendly staff at boarding.",
        "Would recommend to friends and family.",
        "Good legroom and the AC worked well.",
    ];

    private static List<RoutePair> PickRoutesForCompany(Random rng, int count)
    {
        var pool = Routes.ToList();
        var take = Math.Min(count, pool.Count);
        for (var i = 0; i < take; i++)
        {
            var j = rng.Next(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool.GetRange(0, take);
    }

    private static decimal ComputePrice(decimal basePrice, BusType type, Random rng)
    {
        var jitterPercent = rng.Next(-10, 11); // -10..10
        var price = basePrice * PriceMultiplier(type) * (1 + jitterPercent / 100m);
        return Math.Round(price / 500m, MidpointRounding.AwayFromZero) * 500m; // round to nearest 500 SYP
    }

    private static decimal PriceMultiplier(BusType type) => type switch
    {
        BusType.Standard => 1.0m,
        BusType.Premium => 1.3m,
        BusType.Luxury => 1.6m,
        BusType.Sleeper => 1.8m,
        _ => 1.0m,
    };

    private static BusType PickBusType(Random rng) => rng.Next(100) switch
    {
        < 45 => BusType.Standard,
        < 75 => BusType.Premium,
        < 92 => BusType.Luxury,
        _ => BusType.Sleeper,
    };

    private static (string First, string Last) PickName(Random rng) =>
        (FirstNames[rng.Next(FirstNames.Length)], LastNames[rng.Next(LastNames.Length)]);

    private static decimal SyntheticRefundFraction(Random rng)
    {
        // No real "cancelled at" timestamp exists for backdated demo data, so a past cancellation's
        // refund tier is drawn from a distribution shaped like RefundPolicy's real outcome mix
        // (most cancellations happen well before departure — full refund).
        var roll = rng.NextDouble();
        if (roll < 0.6) return 1.0m;
        if (roll < 0.85) return 0.5m;
        return 0m;
    }

    private static List<int> PickFreeSeats(Random rng, int seatCount, HashSet<int> used, int count)
    {
        var picked = new List<int>(count);
        for (var i = 0; i < count && used.Count < seatCount; i++)
        {
            var seat = rng.Next(1, seatCount + 1);
            var attempts = 0;
            while (used.Contains(seat) && attempts < 20)
            {
                seat = rng.Next(1, seatCount + 1);
                attempts++;
            }
            if (used.Contains(seat))
                seat = Enumerable.Range(1, seatCount).First(s => !used.Contains(s));

            used.Add(seat);
            picked.Add(seat);
        }
        return picked;
    }

    private static string Slugify(string name)
    {
        Span<char> buffer = stackalloc char[name.Length];
        var idx = 0;
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
                buffer[idx++] = char.ToLowerInvariant(c);
        }
        return new string(buffer[..idx]);
    }

    // ── Nested data-carrying helper types (seeder-internal only) ───────────────────────────

    private sealed record CompanySpec(string Name, string Phone, string ManagerEmail, string ManagerName, string Prefix);

    private sealed record RoutePair(string Origin, string Destination, double DurationHours, decimal BasePrice);

    private sealed record PendingCancellation(Booking Booking, Payment Payment, decimal RefundFraction, string Reason);

    private sealed record ReviewCandidate(Booking Booking, Trip Trip, bool Force);

    private sealed class DemoCustomerQuota
    {
        public int PastRemaining { get; set; } = 3;
        public int FutureRemaining { get; set; } = 1;
    }

    private sealed class SeedTotals
    {
        public int Companies { get; set; }
        public int Buses { get; set; }
        public int Drivers { get; set; }
        public int Staff { get; set; }
        public int Trips { get; set; }
        public int Bookings { get; set; }
        public int Passengers { get; set; }
        public int Payments { get; set; }
        public int Refunds { get; set; }
        public int Reviews { get; set; }
        public int PromoCodes { get; set; }
        public int CancelledBookings { get; set; }
        public int NoShowBookings { get; set; }
    }

    private sealed class CompanySeedContext
    {
        public List<Trip> ToCancelTrips { get; } = [];
        public List<Trip> ToComplete { get; } = [];
        public List<PendingCancellation> ToCancel { get; } = [];
        public List<Booking> ToMarkNoShow { get; } = [];
        public List<ReviewCandidate> ReviewCandidates { get; } = [];
    }

    private static readonly Action<ILogger, int, int, int, int, int, int, Exception?> LogSeeded =
        LoggerMessage.Define<int, int, int, int, int, int>(LogLevel.Information, new EventId(1, nameof(LogSeeded)),
            "Seeded demo data: {Companies} companies, {Trips} trips, {Bookings} bookings ({Passengers} passengers), {Payments} payments, {Reviews} reviews.");

    private static readonly Action<ILogger, string, string, Exception?> LogUserFailed =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(2, nameof(LogUserFailed)),
            "Demo user {Email} not created ({Errors}).");

    private static readonly Action<ILogger, Exception?> LogSeedFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3, nameof(LogSeedFailed)),
            "Demo data seeding skipped (is the database migrated and reachable?).");
}
