/**
 * Development environment. `ng serve` proxies `/api` to the backend (see proxy.conf.json),
 * so requests are same-origin with the dev server — the refresh cookie and interceptors work
 * exactly as they will in production.
 */
export const environment = {
  production: false,
  apiBaseUrl: '/api',
};
