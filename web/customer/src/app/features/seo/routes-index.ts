import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { DecimalPipe, isPlatformBrowser } from '@angular/common';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorBus, phosphorArrowRight } from '@ng-icons/phosphor-icons/regular';
import { SeoApiService, SeoRoute } from '../../core/seo/seo-api.service';
import { SeoService } from '../../core/seo/seo.service';
import { seedRouteSlugs } from '../../core/seo/seo-seed';

/**
 * /routes — the routes hub. Lists every bookable origin→destination pair as a real <a> link to its
 * /bus/{slug} page, so crawlers discover and follow every route (no orphans) and users have a
 * browsable index. Prerendered.
 */
@Component({
  selector: 'app-routes-index',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DecimalPipe, NgIcon],
  providers: [provideIcons({ phosphorBus, phosphorArrowRight })],
  template: `
    <section class="py-6">
      <nav aria-label="Breadcrumb" class="text-sm text-slate-500 dark:text-slate-400">
        <a routerLink="/" class="hover:text-brand-600">Home</a>
        <span class="mx-1.5">/</span>
        <span class="text-slate-700 dark:text-slate-200">Routes</span>
      </nav>

      <h1 class="mt-3 text-3xl font-extrabold text-slate-900 dark:text-slate-100 sm:text-4xl">
        Bus routes across Syria
      </h1>
      <p class="mt-3 max-w-2xl text-slate-600 dark:text-slate-300">
        Browse every intercity bus route on TPX Travel. Compare operators, see fares from the
        cheapest available seat, and book your ticket online in under a minute.
      </p>

      @if (loading()) {
        <div class="mt-8 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          @for (s of [1, 2, 3, 4, 5, 6]; track s) {
            <div class="card h-20 animate-pulse bg-slate-100 dark:bg-white/5"></div>
          }
        </div>
      } @else {
        @for (group of grouped(); track group.origin) {
          <section class="mt-8">
            <h2 class="text-xl font-bold text-slate-900 dark:text-slate-100">
              From {{ group.origin }}
            </h2>
            <div class="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              @for (r of group.routes; track r.slug) {
                <a
                  [routerLink]="['/bus', r.slug]"
                  class="card group flex items-center justify-between gap-3 p-4 transition hover:-translate-y-0.5 hover:shadow-glow"
                >
                  <span class="flex items-center gap-3">
                    <span class="grid h-10 w-10 shrink-0 place-items-center rounded-2xl bg-linear-to-br from-brand-500 to-brand-700 text-white">
                      <ng-icon name="phosphorBus" />
                    </span>
                    <span>
                      <span class="block font-semibold text-slate-900 dark:text-slate-100">
                        {{ r.origin }} → {{ r.destination }}
                      </span>
                      @if (r.minPrice > 0) {
                        <span class="block text-xs text-slate-500 dark:text-slate-400">
                          from {{ r.minPrice | number: '1.0-0' }} {{ r.currency }}
                        </span>
                      }
                    </span>
                  </span>
                  <ng-icon name="phosphorArrowRight" class="text-brand-500 transition group-hover:translate-x-0.5" />
                </a>
              }
            </div>
          </section>
        }
      }
    </section>
  `,
})
export class RoutesIndexComponent implements OnInit {
  private readonly seoApi = inject(SeoApiService);
  private readonly seo = inject(SeoService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly loading = signal(true);
  protected readonly grouped = signal<{ origin: string; routes: SeoRoute[] }[]>([]);

  ngOnInit(): void {
    this.seo.set({
      title: 'Bus Routes Across Syria — Schedules & Tickets | TPX Travel',
      description:
        'Browse every intercity bus route in Syria. Compare operators, check fares from the cheapest seat, and book Damascus, Aleppo, Homs, Latakia & more online with TPX Travel.',
      path: '/routes',
      type: 'website',
    });

    this.seo.setJsonLd('breadcrumb', {
      '@context': 'https://schema.org',
      '@type': 'BreadcrumbList',
      itemListElement: [
        { '@type': 'ListItem', position: 1, name: 'Home', item: this.seo.abs('/') },
        { '@type': 'ListItem', position: 2, name: 'Routes', item: this.seo.abs('/routes') },
      ],
    });

    // Prerender: render the seed list synchronously (no network at build time). Browser: fetch live.
    this.render(this.fallback());
    if (this.isBrowser) {
      this.seoApi.routes().subscribe({
        next: (routes) => this.render(routes),
        error: () => undefined, // keep the seed-rendered list
      });
    }
  }

  private render(routes: SeoRoute[]): void {
    const byOrigin = new Map<string, SeoRoute[]>();
    for (const r of routes) {
      const list = byOrigin.get(r.origin) ?? [];
      list.push(r);
      byOrigin.set(r.origin, list);
    }
    const grouped = [...byOrigin.entries()]
      .map(([origin, list]) => ({
        origin,
        routes: list.sort((a, b) => a.destination.localeCompare(b.destination)),
      }))
      .sort((a, b) => a.origin.localeCompare(b.origin));
    this.grouped.set(grouped);
    this.loading.set(false);
  }

  /**
   * Static fallback so the prerendered HTML always lists routes even when the API is down at build
   * time. Derives origin/destination from the seed slugs.
   */
  private fallback(): SeoRoute[] {
    return seedRouteSlugs().map((slug) => {
      const [origin, destination] = slug.split('-to-').map(this.titleCase);
      return { origin, destination, slug, tripCount: 0, minPrice: 0, currency: 'SYP' };
    });
  }

  private titleCase(s: string): string {
    return s
      .split('-')
      .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
      .join(' ');
  }
}
