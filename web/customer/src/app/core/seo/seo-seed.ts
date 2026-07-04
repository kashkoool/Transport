/**
 * Hardcoded fallback data for prerendering. If the backend SEO API is unreachable at build time
 * (it usually is — the API container isn't up during `ng build`), the server route params fall
 * back to these real Syrian city pairs so the build ALWAYS produces route/city pages and never
 * breaks. At runtime the pages re-fetch live data from the API.
 */

/** Major Syrian cities served by the platform. */
export const SEED_CITIES = [
  'Damascus',
  'Aleppo',
  'Homs',
  'Hama',
  'Latakia',
  'Tartus',
  'Deir ez-Zor',
] as const;

/** Lowercase, hyphenated slug from a city name (matches the backend's slug convention). */
export function citySlug(city: string): string {
  return city
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/(^-|-$)/g, '');
}

/** `{origin}-to-{destination}` route slug (matches the backend, e.g. `damascus-to-aleppo`). */
export function routeSlug(origin: string, destination: string): string {
  return `${citySlug(origin)}-to-${citySlug(destination)}`;
}

/** All directed city pairs among the seed cities → route slugs (both directions). */
export function seedRouteSlugs(): string[] {
  const slugs: string[] = [];
  for (const from of SEED_CITIES) {
    for (const to of SEED_CITIES) {
      if (from !== to) slugs.push(routeSlug(from, to));
    }
  }
  return slugs;
}

/** Seed city slugs. */
export function seedCitySlugs(): string[] {
  return SEED_CITIES.map(citySlug);
}
