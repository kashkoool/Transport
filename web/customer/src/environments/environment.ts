/**
 * Production environment. The API is served same-origin behind the same reverse proxy
 * that serves this SPA, so a relative `/api` base needs no CORS and lets the SameSite=Strict
 * refresh cookie work without a cross-site exception. For a split-domain deployment, set this
 * to the absolute API origin (e.g. 'https://api.example.com/api') and add that origin to the
 * backend's Cors:AllowedOrigins.
 */
export const environment = {
  production: true,
  apiBaseUrl: '/api',
  // Canonical public origin used for <link rel=canonical>, og:url and absolute OG image URLs.
  // Set this to the real production host at deploy time (e.g. 'https://tpxtravel.sy').
  siteBaseUrl: 'http://localhost:8080',
};
