import http from 'k6/http';
import { check, sleep } from 'k6';

// k6 baseline load test (Tier 3 smoke / Tier 4 soak). Points at a running API via BASE_URL.
//   Smoke:  k6 run tests/load/baseline.js -e BASE_URL=https://staging.example.com
//   Soak:   k6 run tests/load/baseline.js -e BASE_URL=... -e PROFILE=soak
//
// It exercises only anonymous, read-only endpoints (health + trip search) so it never mutates
// data — safe to run against staging. The search route is the most DoS-prone (anonymous,
// JOIN-heavy), which is exactly what we want to load.
const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const PROFILE = __ENV.PROFILE || 'smoke';

const profiles = {
  // Quick confidence check on every staging deploy.
  smoke: {
    stages: [
      { duration: '30s', target: 10 },
      { duration: '1m', target: 10 },
      { duration: '30s', target: 0 },
    ],
  },
  // Sustained load to surface leaks / GC pressure / connection-pool exhaustion over time.
  soak: {
    stages: [
      { duration: '2m', target: 40 },
      { duration: '30m', target: 40 },
      { duration: '3m', target: 0 },
    ],
  },
};

export const options = {
  scenarios: {
    default: { executor: 'ramping-vus', startVUs: 0, ...profiles[PROFILE] },
  },
  thresholds: {
    // Fail the run if the SLOs regress.
    http_req_failed: ['rate<0.01'], // <1% errors
    http_req_duration: ['p(95)<800'], // p95 under 800ms
  },
};

// A seeded/known route; adjust date to an upcoming departure on the target environment.
const searchDate = new Date(Date.now() + 86_400_000).toISOString().slice(0, 10);

export default function () {
  const health = http.get(`${BASE_URL}/health`);
  check(health, { 'health 200': (r) => r.status === 200 });

  const search = http.get(
    `${BASE_URL}/api/trips/search?origin=Damascus&destination=Latakia&date=${searchDate}`,
  );
  check(search, { 'search ok': (r) => r.status === 200 });

  sleep(1);
}
