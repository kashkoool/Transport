import { RenderMode, ServerRoute } from '@angular/ssr';
import { seedCitySlugs, seedRouteSlugs } from './core/seo/seo-seed';

/**
 * Build-time base URL for the SEO API. During `ng build` the API container is usually not up, so
 * these fetches are EXPECTED to fail; when they do we fall back to a hardcoded seed of real
 * Syrian city pairs. A failed fetch must NEVER break the build.
 */
const SEO_API = process.env['SEO_API_BASE'] || 'http://localhost:8080/api/seo';

async function fetchSlugs(
  endpoint: string,
  pick: (row: { slug: string }) => string,
  fallback: () => string[],
  label: string,
): Promise<string[]> {
  try {
    const res = await fetch(`${SEO_API}/${endpoint}`, {
      signal: AbortSignal.timeout(5000),
    });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const rows = (await res.json()) as { slug: string }[];
    const slugs = rows.map(pick).filter(Boolean);
    if (slugs.length === 0) throw new Error('empty response');
    console.log(`[prerender] ${label}: fetched ${slugs.length} slugs from API`);
    return slugs;
  } catch (err) {
    const seeds = fallback();
    console.log(
      `[prerender] ${label}: API fetch failed (${(err as Error).message}); using ${seeds.length} seed slugs`,
    );
    return seeds;
  }
}

export const serverRoutes: ServerRoute[] = [
  // ── Public SEO pages: prerendered to static HTML ──
  { path: '', renderMode: RenderMode.Prerender },
  { path: 'routes', renderMode: RenderMode.Prerender },
  {
    path: 'bus/:route',
    renderMode: RenderMode.Prerender,
    async getPrerenderParams() {
      const slugs = await fetchSlugs(
        'routes',
        (r) => r.slug,
        seedRouteSlugs,
        'bus/:route',
      );
      return slugs.map((route) => ({ route }));
    },
  },
  {
    path: 'city/:city',
    renderMode: RenderMode.Prerender,
    async getPrerenderParams() {
      const slugs = await fetchSlugs(
        'cities',
        (c) => c.slug,
        seedCitySlugs,
        'city/:city',
      );
      return slugs.map((city) => ({ city }));
    },
  },

  // ── Everything else (auth, account, vendor, admin, booking flow) is client-rendered only.
  // These carry noindex via SeoService and never need a prerendered shell. ──
  { path: '**', renderMode: RenderMode.Client },
];
