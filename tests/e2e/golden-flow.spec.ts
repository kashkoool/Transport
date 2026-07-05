import { test, expect } from '@playwright/test';

// The seeded customer + Damascus→Latakia trips come from DevDataSeeder (Development only), which
// seeds departures on each of the next 3 days. Default to tomorrow (UTC) so the test never rots;
// override with TRIP_DATE=YYYY-MM-DD if needed.
const CUSTOMER = { email: 'customer@tpx.local', password: 'Demo!Passw0rd' };
const tomorrowUtc = () => {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() + 1);
  return d.toISOString().slice(0, 10);
};
const TRIP_DATE = process.env.TRIP_DATE ?? tomorrowUtc();

test('customer golden flow: login → search → seat → book → pay → ticket', async ({ page }) => {
  // 1) Log in as the seeded customer.
  await page.goto('/login');
  await page.fill('#email', CUSTOMER.email);
  await page.fill('#password', CUSTOMER.password);
  await page.click('button[type="submit"]');
  await expect(page).not.toHaveURL(/\/login/);

  // 2) Search the seeded route/date.
  await page.goto('/');
  await page.fill('#origin', 'Damascus');
  await page.fill('#destination', 'Latakia');
  await page.fill('#date', TRIP_DATE);
  await page.click('button[type="submit"]');

  const selectSeats = page.getByRole('button', { name: /select seats/i }).first();
  await expect(selectSeats).toBeVisible();
  await selectSeats.click();

  // 3) Booking page: pick the first free seat, then hold it.
  await expect(page).toHaveURL(/\/book\//);
  const seatSection = page.locator('section', {
    has: page.getByRole('heading', { name: /choose your seats/i }),
  });
  await seatSection.locator('button:not([disabled])').first().click();
  await page.getByRole('button', { name: /hold \d+ seat/i }).click();

  // 4) Passenger details → confirm.
  await page.getByPlaceholder('First name').first().fill('E2E');
  await page.getByPlaceholder('Last name').first().fill('Tester');
  await page.getByRole('button', { name: /confirm .* continue to payment/i }).click();

  // 5) Payment. The prod-built SPA hands off to the gateway's EXTERNAL hosted page (the in-page
  //    "Pay now" stand-in only exists in a dev build), so there is no deterministic UI button to
  //    click. Confirm the pay page rendered, then complete the gateway callback the way the app
  //    does under the hood — checkout + the signed /dev/simulate webhook. Those calls need the
  //    in-memory access token as a Bearer header (only the refresh token is a cookie), so log in
  //    once via the API to obtain it.
  await expect(page).toHaveURL(/\/pay\//);
  await expect(page.getByRole('button', { name: /proceed to payment/i })).toBeVisible();
  const bookingId = page.url().match(/\/pay\/([0-9a-f-]+)/i)![1];
  const reference = (await page.locator('span.font-mono').first().textContent())!.trim();

  const login = await page.request.post('/api/auth/login', {
    data: { email: CUSTOMER.email, password: CUSTOMER.password },
  });
  const { accessToken } = await login.json();
  const auth = { Authorization: `Bearer ${accessToken}` };

  const checkout = await page.request.post('/api/payments/checkout', { headers: auth, data: { bookingId } });
  expect(checkout.ok()).toBeTruthy();
  const sim = await page.request.post('/api/payments/dev/simulate', {
    headers: auth,
    data: { bookingReference: reference, succeeded: true },
  });
  expect(sim.ok()).toBeTruthy();
  expect((await sim.json()).status).toBe('confirmed');

  // 6) Ticket issued and Confirmed (rendered by the real ticket page).
  await page.goto(`/ticket/${bookingId}`);
  await expect(page.getByText(/boarding ticket/i)).toBeVisible();
  await expect(page.getByText('Confirmed', { exact: true })).toBeVisible();
});
