import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe, DecimalPipe, isPlatformBrowser } from '@angular/common';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  phosphorBus,
  phosphorClock,
  phosphorBuildings,
  phosphorTag,
  phosphorMapTrifold,
} from '@ng-icons/phosphor-icons/regular';
import { SeoApiService, SeoRouteDetail } from '../../core/seo/seo-api.service';
import { SeoService } from '../../core/seo/seo.service';

/**
 * /bus/:route — the primary SEO landing (money page). Targets the long-tail query
 * "bus {origin} to {destination}". Prerendered per slug; re-fetches live departures at runtime.
 * Unknown slugs render a not-found state AND emit noindex so soft-404s can't be indexed.
 */
@Component({
  selector: 'app-route-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, DecimalPipe, NgIcon],
  providers: [
    provideIcons({ phosphorBus, phosphorClock, phosphorBuildings, phosphorTag, phosphorMapTrifold }),
  ],
  template: `
    @if (notFound()) {
      <section class="py-16 text-center">
        <ng-icon name="phosphorMapTrifold" class="text-5xl text-slate-300 dark:text-slate-600" />
        <h1 class="mt-4 text-2xl font-bold text-slate-900 dark:text-slate-100">Route not found</h1>
        <p class="mt-2 text-slate-500 dark:text-slate-400">
          We couldn't find that route. Browse all available routes instead.
        </p>
        <a routerLink="/routes" class="btn btn-primary mt-6">See all routes</a>
      </section>
    } @else if (detail(); as d) {
      <article class="py-6">
        <nav aria-label="Breadcrumb" class="text-sm text-slate-500 dark:text-slate-400">
          <a routerLink="/" class="hover:text-brand-600">Home</a>
          <span class="mx-1.5">/</span>
          <a routerLink="/routes" class="hover:text-brand-600">Routes</a>
          <span class="mx-1.5">/</span>
          <span class="text-slate-700 dark:text-slate-200">{{ d.origin }} → {{ d.destination }}</span>
        </nav>

        <h1 class="mt-3 text-3xl font-extrabold text-slate-900 dark:text-slate-100 sm:text-4xl">
          Bus from {{ d.origin }} to {{ d.destination }}
        </h1>

        <div class="mt-4 flex flex-wrap items-center gap-3 text-sm">
          @if (d.minPrice > 0) {
            <span class="badge badge-brand"><ng-icon name="phosphorTag" /> From {{ d.minPrice | number: '1.0-0' }} {{ d.currency }}</span>
          }
          @if (d.avgDurationMinutes) {
            <span class="badge badge-muted"><ng-icon name="phosphorClock" /> ~{{ durationLabel(d.avgDurationMinutes) }}</span>
          }
          @if (d.companies.length) {
            <span class="badge badge-muted"><ng-icon name="phosphorBuildings" /> {{ d.companies.length }} operator{{ d.companies.length > 1 ? 's' : '' }}</span>
          }
        </div>

        <p class="mt-5 max-w-2xl text-slate-600 dark:text-slate-300">
          Book your bus ticket from {{ d.origin }} to {{ d.destination }} online with TPX Travel.
          Compare every operator on this route, choose your exact seat on a live seat map, and pay
          securely to get an instant QR ticket. Fares start
          {{ d.minPrice > 0 ? 'from ' + (d.minPrice | number: '1.0-0') + ' ' + d.currency : 'at competitive rates' }}.
        </p>

        <!-- Prominent CTA into the real search -->
        <a [routerLink]="['/']" [queryParams]="searchParams()" class="btn btn-primary mt-6 h-12 px-7 text-base">
          Search {{ d.origin }} → {{ d.destination }} buses
        </a>

        <!-- Next departures -->
        @if (d.next.length) {
          <section class="mt-12">
            <h2 class="text-2xl font-bold text-slate-900 dark:text-slate-100">Next departures</h2>
            <div class="mt-4 space-y-3">
              @for (dep of d.next; track dep.departureUtc) {
                <div class="card flex flex-col gap-3 p-4 sm:flex-row sm:items-center sm:justify-between">
                  <div class="flex items-center gap-3">
                    <span class="grid h-10 w-10 shrink-0 place-items-center rounded-2xl bg-linear-to-br from-brand-500 to-brand-700 text-white"><ng-icon name="phosphorBus" /></span>
                    <div>
                      <p class="font-semibold text-slate-900 dark:text-slate-100">
                        {{ dep.departureUtc | date: 'EEE, MMM d, HH:mm' }} · arrives {{ dep.arrivalUtc | date: 'HH:mm' }}
                      </p>
                      <p class="text-sm text-slate-500 dark:text-slate-400">{{ dep.companyName }}</p>
                    </div>
                  </div>
                  <div class="text-right">
                    <span class="text-lg font-bold text-slate-900 dark:text-slate-100">{{ dep.price | number: '1.0-0' }} {{ dep.currency }}</span>
                  </div>
                </div>
              }
            </div>
          </section>
        }

        <!-- Operators -->
        @if (d.companies.length) {
          <section class="mt-12">
            <h2 class="text-2xl font-bold text-slate-900 dark:text-slate-100">Bus operators on this route</h2>
            <div class="mt-4 flex flex-wrap gap-2">
              @for (c of d.companies; track c) {
                <span class="badge badge-muted">{{ c }}</span>
              }
            </div>
          </section>
        }

        <!-- FAQ -->
        <section class="mt-12">
          <h2 class="text-2xl font-bold text-slate-900 dark:text-slate-100">Frequently asked questions</h2>
          <div class="mt-4 space-y-3">
            @for (f of faq(); track f.q) {
              <details class="card p-4">
                <summary class="cursor-pointer font-semibold text-slate-900 dark:text-slate-100">{{ f.q }}</summary>
                <p class="mt-2 text-sm text-slate-600 dark:text-slate-300">{{ f.a }}</p>
              </details>
            }
          </div>
        </section>

        <!-- Closing CTA -->
        <section class="mt-12">
          <div class="rounded-3xl bg-linear-to-br from-brand-700 to-brand-500 px-6 py-10 text-center text-white">
            <h2 class="text-2xl font-bold">Ready to travel {{ d.origin }} → {{ d.destination }}?</h2>
            <p class="mx-auto mt-2 max-w-md text-white/80">Pick your date and seat now — you'll have a QR ticket in under a minute.</p>
            <a [routerLink]="['/']" [queryParams]="searchParams()" class="btn btn-accent mt-6">Find your trip</a>
          </div>
        </section>
      </article>
    } @else {
      <div class="space-y-4 py-8">
        <div class="card h-10 w-2/3 animate-pulse bg-slate-100 dark:bg-white/5"></div>
        <div class="card h-40 animate-pulse bg-slate-100 dark:bg-white/5"></div>
      </div>
    }
  `,
})
export class RoutePageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly seoApi = inject(SeoApiService);
  private readonly seo = inject(SeoService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly detail = signal<SeoRouteDetail | null>(null);
  protected readonly notFound = signal(false);

  protected readonly searchParams = computed(() => {
    const d = this.detail();
    return d ? { origin: d.origin, destination: d.destination } : {};
  });

  ngOnInit(): void {
    const slug = this.route.snapshot.paramMap.get('route') ?? '';

    // Render a slug-derived detail immediately so the PRERENDERED HTML already has the <h1>,
    // metadata and JSON-LD — no network needed at build time (the API is unreachable then, and an
    // HTTP call during prerender throws an uncaught error that aborts the render).
    const seed = this.fromSlug(slug);
    if (seed) this.apply(seed);
    else this.markNotFound(slug);

    // In the browser only, fetch live data to enrich departures/operators/prices. On any error
    // (including a 404 while the SEO API is not yet deployed, or a transient failure) we KEEP the
    // slug-derived indexable page rather than tearing it down — a well-formed slug that reached
    // this component was already validated by fromSlug(), so a missing API row must not silently
    // noindex a real route. Genuinely malformed slugs never get here (fromSlug returns null above).
    if (this.isBrowser && seed) {
      this.seoApi.route(slug).subscribe({
        next: (d) => this.apply(d),
        error: () => undefined,
      });
    }
  }

  /** Build a minimal detail from the slug so prerender has real, indexable content. */
  private fromSlug(slug: string): SeoRouteDetail | null {
    const parts = slug.split('-to-');
    if (parts.length !== 2 || !parts[0] || !parts[1]) return null;
    const [origin, destination] = parts.map((p) => this.titleCase(p));
    return {
      origin,
      destination,
      slug,
      minPrice: 0,
      currency: 'SYP',
      upcomingCount: 0,
      companies: [],
      avgDurationMinutes: null,
      next: [],
    };
  }

  private titleCase(s: string): string {
    return s
      .split('-')
      .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
      .join(' ');
  }

  protected durationLabel(minutes: number): string {
    const h = Math.floor(minutes / 60);
    const m = minutes % 60;
    return h > 0 ? `${h}h${m ? ' ' + m + 'm' : ''}` : `${m}m`;
  }

  protected faq(): { q: string; a: string }[] {
    const d = this.detail();
    const o = d?.origin ?? 'the origin';
    const dest = d?.destination ?? 'the destination';
    const price =
      d && d.minPrice > 0 ? `from ${d.minPrice} ${d.currency}` : 'at competitive fares';
    return [
      {
        q: `How much is a bus ticket from ${o} to ${dest}?`,
        a: `Tickets on the ${o} to ${dest} route start ${price}. The exact fare depends on the operator, bus type and how early you book.`,
      },
      {
        q: `How long does the ${o} to ${dest} bus take?`,
        a:
          d && d.avgDurationMinutes
            ? `The journey takes about ${this.durationLabel(d.avgDurationMinutes)} on average, depending on the operator and road conditions.`
            : `Journey time varies by operator and road conditions. Exact durations are shown for each departure when you search.`,
      },
      {
        q: `Can I choose my seat and pay online?`,
        a: `Yes. TPX Travel shows a live seat map for every bus so you pick your exact seat, then pay securely online and receive an instant QR ticket.`,
      },
    ];
  }

  private apply(d: SeoRouteDetail): void {
    this.detail.set(d);
    this.notFound.set(false);

    const title = `Bus ${d.origin} to ${d.destination} — Schedules & Tickets | TPX Travel`;
    const description =
      `Book bus tickets from ${d.origin} to ${d.destination}` +
      (d.minPrice > 0 ? ` from ${d.minPrice} ${d.currency}` : '') +
      `. Compare operators, pick your seat on a live map, and pay securely online with TPX Travel.`;

    this.seo.set({
      title: title.slice(0, 65),
      description: description.slice(0, 160),
      path: `/bus/${d.slug}`,
      type: 'product',
    });

    this.seo.setJsonLd('breadcrumb', {
      '@context': 'https://schema.org',
      '@type': 'BreadcrumbList',
      itemListElement: [
        { '@type': 'ListItem', position: 1, name: 'Home', item: this.seo.abs('/') },
        { '@type': 'ListItem', position: 2, name: 'Routes', item: this.seo.abs('/routes') },
        {
          '@type': 'ListItem',
          position: 3,
          name: `${d.origin} to ${d.destination}`,
          item: this.seo.abs(`/bus/${d.slug}`),
        },
      ],
    });

    this.seo.setJsonLd('product', {
      '@context': 'https://schema.org',
      '@type': 'Product',
      name: `Bus ticket: ${d.origin} to ${d.destination}`,
      description: `Intercity bus from ${d.origin} to ${d.destination} in Syria, booked via TPX Travel.`,
      brand: { '@type': 'Brand', name: 'TPX Travel' },
      ...(d.minPrice > 0
        ? {
            offers: {
              '@type': 'AggregateOffer',
              lowPrice: d.minPrice,
              priceCurrency: d.currency,
              offerCount: d.upcomingCount || d.next.length || 1,
              availability: 'https://schema.org/InStock',
            },
          }
        : {}),
    });

    this.seo.setJsonLd('faq', {
      '@context': 'https://schema.org',
      '@type': 'FAQPage',
      mainEntity: this.faq().map((f) => ({
        '@type': 'Question',
        name: f.q,
        acceptedAnswer: { '@type': 'Answer', text: f.a },
      })),
    });
  }

  private markNotFound(slug: string): void {
    this.detail.set(null);
    this.notFound.set(true);
    // Soft-404 defense: a static file is served at HTTP 200, so noindex is what keeps an unknown
    // slug out of the index.
    this.seo.set({
      title: 'Route not found | TPX Travel',
      description: 'This bus route could not be found. Browse all available routes on TPX Travel.',
      path: `/bus/${slug}`,
    });
    this.seo.noindex();
  }
}
