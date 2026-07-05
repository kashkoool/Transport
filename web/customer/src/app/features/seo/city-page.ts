import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DecimalPipe, isPlatformBrowser } from '@angular/common';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorBus, phosphorArrowRight, phosphorMapTrifold } from '@ng-icons/phosphor-icons/regular';
import { SeoApiService, SeoRoute } from '../../core/seo/seo-api.service';
import { SeoService } from '../../core/seo/seo.service';
import { citySlug, SEED_CITIES, routeSlug } from '../../core/seo/seo-seed';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/**
 * /city/:city — a city hub. Lists every route that touches this city (as origin or destination) as
 * real <a> links to the route pages. Prerendered per city slug. Unknown city → not-found + noindex.
 */
@Component({
  selector: 'app-city-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DecimalPipe, NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorBus, phosphorArrowRight, phosphorMapTrifold })],
  template: `
    @if (notFound()) {
      <section class="py-16 text-center">
        <ng-icon name="phosphorMapTrifold" class="text-5xl text-slate-300 dark:text-slate-600" />
        <h1 class="mt-4 text-2xl font-bold text-slate-900 dark:text-slate-100">{{ 'seo.city.notFound.title' | t }}</h1>
        <p class="mt-2 text-slate-500 dark:text-slate-400">{{ 'seo.city.notFound.body' | t }}</p>
        <a routerLink="/routes" class="btn btn-primary mt-6">{{ 'seo.city.notFound.cta' | t }}</a>
      </section>
    } @else {
      <section class="py-6">
        <nav aria-label="Breadcrumb" class="text-sm text-slate-500 dark:text-slate-400">
          <a routerLink="/" class="hover:text-brand-600">{{ 'seo.breadcrumb.home' | t }}</a>
          <span class="mx-1.5">/</span>
          <a routerLink="/routes" class="hover:text-brand-600">{{ 'seo.breadcrumb.routes' | t }}</a>
          <span class="mx-1.5">/</span>
          <span class="text-slate-700 dark:text-slate-200">{{ cityName() }}</span>
        </nav>

        <h1 class="mt-3 text-3xl font-extrabold text-slate-900 dark:text-slate-100 sm:text-4xl">
          {{ 'seo.city.h1' | t: { city: cityName() } }}
        </h1>
        <p class="mt-3 max-w-2xl text-slate-600 dark:text-slate-300">
          {{ 'seo.city.intro' | t: { city: cityName() } }}
        </p>

        @if (loading()) {
          <div class="mt-8 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            @for (s of [1, 2, 3, 4]; track s) {
              <div class="card h-20 animate-pulse bg-slate-100 dark:bg-white/5"></div>
            }
          </div>
        } @else if (routes().length === 0) {
          <p class="mt-8 text-slate-500 dark:text-slate-400">{{ 'seo.city.empty' | t }}</p>
        } @else {
          <div class="mt-8 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            @for (r of routes(); track r.slug) {
              <a
                [routerLink]="['/bus', r.slug]"
                class="card group flex items-center justify-between gap-3 p-4 transition hover:-translate-y-0.5 hover:shadow-glow"
              >
                <span class="flex items-center gap-3">
                  <span class="grid h-10 w-10 shrink-0 place-items-center rounded-2xl bg-linear-to-br from-brand-500 to-brand-700 text-white">
                    <ng-icon name="phosphorBus" />
                  </span>
                  <span>
                    <span class="block font-semibold text-slate-900 dark:text-slate-100">{{ r.origin }} → {{ r.destination }}</span>
                    @if (r.minPrice > 0) {
                      <span class="block text-xs text-slate-500 dark:text-slate-400">{{ 'seo.city.priceFrom' | t: { price: (r.minPrice | number: '1.0-0'), currency: r.currency } }}</span>
                    }
                  </span>
                </span>
                <ng-icon name="phosphorArrowRight" class="text-brand-500 transition group-hover:translate-x-0.5" />
              </a>
            }
          </div>
        }
      </section>
    }
  `,
})
export class CityPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly seoApi = inject(SeoApiService);
  private readonly seo = inject(SeoService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly routes = signal<SeoRoute[]>([]);
  protected readonly cityName = signal('');

  ngOnInit(): void {
    const slug = this.route.snapshot.paramMap.get('city') ?? '';

    // Prerender: render from the seed list (no network at build time).
    this.apply(slug, this.seedRoutes());

    // Browser: fetch the live routes to refine the list.
    if (this.isBrowser) {
      this.seoApi.routes().subscribe({
        next: (all) => this.apply(slug, all),
        error: () => undefined, // keep the seed-rendered page
      });
    }
  }

  /** All seed city pairs as SeoRoute rows, so the prerendered city page lists real routes. */
  private seedRoutes(): SeoRoute[] {
    const rows: SeoRoute[] = [];
    for (const from of SEED_CITIES) {
      for (const to of SEED_CITIES) {
        if (from !== to) {
          rows.push({
            origin: from,
            destination: to,
            slug: routeSlug(from, to),
            tripCount: 0,
            minPrice: 0,
            currency: 'SYP',
          });
        }
      }
    }
    return rows;
  }

  private apply(slug: string, all: SeoRoute[]): void {
    const matching = all.filter(
      (r) => citySlug(r.origin) === slug || citySlug(r.destination) === slug,
    );

    if (matching.length === 0) {
      // Only flag not-found if we have nothing rendered yet. A later (browser) refresh that returns
      // no rows for an already-rendered seed city must not tear down a valid, indexable page.
      if (this.routes().length === 0) this.markNotFound(slug);
      return;
    }

    // Derive the display name from a matching route (correct casing), falling back to the slug.
    const name =
      matching.find((r) => citySlug(r.origin) === slug)?.origin ??
      matching.find((r) => citySlug(r.destination) === slug)?.destination ??
      this.titleCase(slug);
    this.cityName.set(name);

    // Only show routes departing this city (origin), which read naturally as "routes from {city}".
    const fromCity = matching.filter((r) => citySlug(r.origin) === slug);
    this.routes.set((fromCity.length ? fromCity : matching).sort((a, b) =>
      a.destination.localeCompare(b.destination),
    ));
    this.loading.set(false);
    this.notFound.set(false);

    this.seo.set({
      title: `Bus from ${name} — Routes, Schedules & Tickets | TPX Travel`.slice(0, 65),
      description:
        `Book intercity buses from ${name} across Syria. Compare operators and fares, choose your seat, and pay online with TPX Travel.`.slice(
          0,
          160,
        ),
      path: `/city/${slug}`,
      type: 'website',
    });

    this.seo.setJsonLd('breadcrumb', {
      '@context': 'https://schema.org',
      '@type': 'BreadcrumbList',
      itemListElement: [
        { '@type': 'ListItem', position: 1, name: 'Home', item: this.seo.abs('/') },
        { '@type': 'ListItem', position: 2, name: 'Routes', item: this.seo.abs('/routes') },
        { '@type': 'ListItem', position: 3, name, item: this.seo.abs(`/city/${slug}`) },
      ],
    });
  }

  private markNotFound(slug: string): void {
    this.loading.set(false);
    this.notFound.set(true);
    this.cityName.set(this.titleCase(slug));
    this.seo.set({
      title: 'City not found | TPX Travel',
      description: 'This city could not be found. Browse all available bus routes on TPX Travel.',
      path: `/city/${slug}`,
    });
    this.seo.noindex();
  }

  private titleCase(slug: string): string {
    return slug
      .split('-')
      .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
      .join(' ');
  }
}
