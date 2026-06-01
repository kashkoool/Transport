# TPX Travel — Customer web app

The customer-facing single-page app for the transport booking platform (Angular 20 +
Tailwind v4). It talks to the ASP.NET Core API under `../../src` and walks a customer through
the whole journey: **register/login → search trips → hold seats → book → pay → ticket (QR)**,
plus a **My bookings** view.

## Token model (security)

- The **access token lives in memory only** (an Angular signal) — never `localStorage` /
  `sessionStorage`, so an XSS payload has no persisted token to exfiltrate.
- The **refresh token rides an HttpOnly + Secure + SameSite=Strict cookie** the JS never sees.
  On a hard refresh the app silently re-acquires an access token from that cookie at startup.
- On a `401` the HTTP interceptor performs **one** silent refresh and retries; if that fails the
  user is sent to login.

## Prerequisites

- Node 20+ (developed on Node 24) and npm.
- The backend API running locally — see `../../README` / `../../src/TransportPlatform.Api`.
  A PostgreSQL database must be available and migrated:

  ```sh
  # from transport-platform/
  dotnet ef database update \
    --project src/TransportPlatform.Infrastructure \
    --startup-project src/TransportPlatform.Api
  # run the API on http://localhost:5278 (the port the dev proxy targets)
  dotnet run --project src/TransportPlatform.Api --launch-profile http
  ```

## Run the app

```sh
npm install
npm start          # ng serve on http://localhost:4200
```

`npm start` proxies `/api` to `http://localhost:5278` (see `proxy.conf.json`), so the SPA and
API are same-origin in dev and the refresh cookie just works.

Open <http://localhost:4200> and:

1. **Sign up**, then **Search** a route + date.
2. **Select seats**, hold them, fill passenger names, and **Confirm**.
3. On the payment page, **Pay now** — in development this calls a backend-only sandbox endpoint
   (`/api/payments/dev/simulate`) that signs and posts the gateway webhook, so the booking
   confirms without a real gateway. In production the button hands off to the gateway's hosted
   checkout page instead.
4. See your **ticket with a QR code**, and find it again under **My bookings**.

> You need at least one bookable trip to search for. Trips are created through the vendor/admin
> API (a future increment adds those consoles); seed one via that API or directly in the DB.

## Scripts

- `npm start` — dev server with the API proxy.
- `npm run build` — production build (fails the budget if the bundle grows too large).
- `npm run lint` is `ng lint` — ESLint + Angular template rules.

## Layout

```text
src/app/
  core/        auth (service + interceptor + guard), api service, models, toast, error mapping
  features/    auth (login/register), trips (search), booking, payment, tickets (ticket + my-bookings)
  app.ts       shell (nav + router-outlet + toast)
  app.routes.ts  lazy-loaded routes; everything past search is auth-guarded
```
