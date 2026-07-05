import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { DatePipe, DecimalPipe, isPlatformBrowser } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  phosphorBus,
  phosphorSeat,
  phosphorLockKey,
  phosphorTicket,
  phosphorQrCode,
  phosphorFire,
  phosphorMapTrifold,
} from '@ng-icons/phosphor-icons/regular';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { BookingFlow } from '../../core/booking-flow';
import { PublicCompany, TripSummary } from '../../core/models';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { SeoService } from '../../core/seo/seo.service';
import { SeoApiService, SeoRoute } from '../../core/seo/seo-api.service';
import { seedRouteSlugs } from '../../core/seo/seo-seed';

interface Route {
  from: string;
  to: string;
}

@Component({
  selector: 'app-search',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, DatePipe, DecimalPipe, NgIcon, TranslatePipe],
  providers: [
    provideIcons({
      phosphorBus,
      phosphorSeat,
      phosphorLockKey,
      phosphorTicket,
      phosphorQrCode,
      phosphorFire,
      phosphorMapTrifold,
    }),
  ],
  template: `
    <!-- ░░ HERO — static image with the search content over it ░░ -->
    <section
      class="full-bleed relative flex min-h-[88vh] items-center overflow-hidden bg-ink bg-cover bg-center"
      style="background-image: url('assets/hero.jpg')"
    >
      <!-- Light scrims only (nav top + section blend at bottom); the image stays clearly visible.
           Text legibility comes from text-shadow on the copy, not a heavy overlay. -->
      <div class="pointer-events-none absolute inset-x-0 top-0 h-28 bg-linear-to-b from-ink/45 to-transparent"></div>
      <div class="pointer-events-none absolute inset-x-0 bottom-0 h-44 bg-linear-to-t from-ink/70 to-transparent"></div>

      <!-- Hero content (relative z-10 so it sits above the scrims; over the dark hero → white styling) -->
      <div class="relative z-10 w-full">
        <div class="mx-auto w-full max-w-6xl px-4 py-24 text-white">
          <div class="max-w-2xl text-center [text-shadow:0_2px_18px_rgb(0_0_0_/_0.85)] lg:text-left">
            <span class="badge bg-black/30 text-white ring-1 ring-white/20 backdrop-blur-sm">
              <span class="tracking-[0.2em] text-flag">★★★</span> {{ 'hero.badge' | t }}
            </span>
            <h1 class="mt-5 text-4xl leading-[1.05] sm:text-6xl">
              {{ 'hero.title1' | t }}
              <span class="text-brand-300">{{ 'hero.title2' | t }}</span>
            </h1>
            <p class="mx-auto mt-5 max-w-xl text-base text-white/75 sm:text-lg lg:mx-0">
              {{ 'hero.subtitle' | t }}
            </p>
          </div>

          <!-- Styled search bar (pointer-events-auto to stay usable over the pinned canvas) -->
          <form [formGroup]="form" (ngSubmit)="search()" class="pointer-events-auto mt-9 max-w-3xl">
            <div class="rounded-3xl bg-white p-2 shadow-glow ring-1 ring-black/5 sm:rounded-full">
              <div class="grid gap-1 sm:grid-cols-[1fr_1fr_1fr_auto] sm:items-center">
                <div class="rounded-2xl px-4 py-2 hover:bg-slate-50">
                  <label for="origin" class="block text-[11px] font-bold uppercase tracking-wide text-slate-400">{{ 'common.from' | t }}</label>
                  <input id="origin" formControlName="origin" placeholder="Damascus" class="w-full border-0 bg-transparent p-0 text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-0" />
                </div>
                <div class="rounded-2xl px-4 py-2 hover:bg-slate-50 sm:border-l sm:border-slate-100">
                  <label for="destination" class="block text-[11px] font-bold uppercase tracking-wide text-slate-400">{{ 'common.to' | t }}</label>
                  <input id="destination" formControlName="destination" placeholder="Latakia" class="w-full border-0 bg-transparent p-0 text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-0" />
                </div>
                <div class="rounded-2xl px-4 py-2 hover:bg-slate-50 sm:border-l sm:border-slate-100">
                  <label for="date" class="block text-[11px] font-bold uppercase tracking-wide text-slate-400">{{ 'common.date' | t }}</label>
                  <input id="date" type="date" formControlName="date" class="w-full border-0 bg-transparent p-0 text-slate-900 focus:outline-none focus:ring-0" />
                </div>
                <button type="submit" [disabled]="loading()" class="btn btn-primary m-1 h-12 px-7">
                  @if (loading()) {
                    <span class="h-4 w-4 animate-spin rounded-full border-2 border-white/40 border-t-white"></span>
                  } @else {
                    <span>{{ 'common.search' | t }}</span>
                  }
                </button>
              </div>
            </div>

            <details class="mt-3 max-w-3xl">
              <summary class="cursor-pointer select-none text-center text-sm font-medium text-white/70 hover:text-white lg:text-left">
                {{ 'hero.advanced' | t }}
              </summary>
              <div class="mt-3 grid gap-3 rounded-2xl bg-white p-3 shadow-soft sm:grid-cols-3">
                <div>
                  <label for="company" class="mb-1.5 block text-sm font-semibold text-slate-700">Company</label>
                  <select id="company" formControlName="companyId" class="w-full rounded-xl border-0 bg-slate-50 px-3 py-2 text-slate-900 ring-1 ring-inset ring-slate-200 focus:ring-2 focus:ring-inset focus:ring-brand-500">
                    <option value="">Any company</option>
                    @for (c of companies(); track c.id) {
                      <option [value]="c.id">{{ c.name }}</option>
                    }
                  </select>
                </div>
                <div>
                  <label for="maxPrice" class="mb-1.5 block text-sm font-semibold text-slate-700">Max price</label>
                  <input id="maxPrice" type="number" min="0" step="1000" formControlName="maxPrice" placeholder="Any" class="w-full rounded-xl border-0 bg-slate-50 px-3 py-2 text-slate-900 ring-1 ring-inset ring-slate-200 focus:ring-2 focus:ring-inset focus:ring-brand-500" />
                </div>
                <div>
                  <label for="departAfter" class="mb-1.5 block text-sm font-semibold text-slate-700">Depart after</label>
                  <input id="departAfter" type="time" formControlName="departAfter" class="w-full rounded-xl border-0 bg-slate-50 px-3 py-2 text-slate-900 ring-1 ring-inset ring-slate-200 focus:ring-2 focus:ring-inset focus:ring-brand-500" />
                </div>
              </div>
            </details>
          </form>

          <div class="mt-8 flex flex-wrap items-center justify-center gap-x-8 gap-y-2 text-sm text-white/80 [text-shadow:0_1px_10px_rgb(0_0_0_/_0.9)] lg:justify-start">
            <span class="inline-flex items-center gap-1.5"><ng-icon name="phosphorBus" /> {{ 'hero.trust.company' | t }}</span>
            <span class="inline-flex items-center gap-1.5"><ng-icon name="phosphorSeat" /> {{ 'hero.trust.seats' | t }}</span>
            <span class="inline-flex items-center gap-1.5"><ng-icon name="phosphorLockKey" /> {{ 'hero.trust.pay' | t }}</span>
            <span class="inline-flex items-center gap-1.5"><ng-icon name="phosphorQrCode" /> {{ 'hero.trust.qr' | t }}</span>
          </div>
        </div>
      </div>
    </section>

    <!-- ░░ BODY ░░ -->
    <div class="py-12">
      @if (!searched()) {
        <!-- Most booked routes -->
        <section>
          <p class="eyebrow">Most booked</p>
          <h2 class="mt-1 text-2xl text-slate-900 dark:text-slate-100 sm:text-3xl">Trending routes this week</h2>
          <div class="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            @for (r of popularRoutes; track r.from + r.to; let i = $index) {
              <button
                type="button"
                (click)="quickRoute(r)"
                class="group relative overflow-hidden rounded-3xl bg-ink p-5 text-left text-white ring-1 ring-white/10 transition hover:-translate-y-0.5 hover:shadow-glow dark:bg-ink-700"
              >
                <div class="absolute inset-0 bg-linear-to-br from-brand-600/50 to-transparent opacity-70 transition group-hover:opacity-100"></div>
                @if (i === 0) {
                  <span class="absolute right-3 top-3 z-10 inline-flex items-center gap-1 rounded-full bg-accent-400 px-2 py-0.5 text-xs font-bold text-ink"><ng-icon name="phosphorFire" /> Hot</span>
                }
                <div class="relative">
                  <p class="text-sm text-white/60">{{ r.from }}</p>
                  <p class="mt-0.5 text-xl font-bold">→ {{ r.to }}</p>
                  <p class="mt-8 text-xs font-semibold text-accent-300">View trips →</p>
                </div>
              </button>
            }
          </div>
        </section>

        <!-- Popular routes — real <a> links so crawlers follow them to the /bus/{slug} pages -->
        <section class="mt-16">
          <div class="flex items-end justify-between gap-4">
            <div>
              <p class="eyebrow">Popular routes</p>
              <h2 class="mt-1 text-2xl text-slate-900 dark:text-slate-100 sm:text-3xl">Book a bus route</h2>
            </div>
            <a routerLink="/routes" class="btn btn-ghost shrink-0">All routes →</a>
          </div>
          <div class="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            @for (r of seoRoutes(); track r.slug) {
              <a
                [routerLink]="['/bus', r.slug]"
                class="card group flex items-center justify-between gap-2 p-4 transition hover:-translate-y-0.5 hover:shadow-glow"
              >
                <span class="font-semibold text-slate-900 dark:text-slate-100">{{ r.origin }} → {{ r.destination }}</span>
                <span class="text-brand-500 transition group-hover:translate-x-0.5">→</span>
              </a>
            }
          </div>
        </section>

        <!-- Why TPX -->
        <section class="mt-16">
          <h2 class="text-2xl text-slate-900 dark:text-slate-100 sm:text-3xl">Everything in a couple of taps</h2>
          <div class="mt-6 grid gap-4 sm:grid-cols-3">
            @for (f of features; track f.title) {
              <div class="card p-6">
                <span class="grid h-12 w-12 place-items-center rounded-2xl bg-brand-50 text-2xl text-brand-600"><ng-icon [name]="f.icon" /></span>
                <p class="mt-4 text-lg font-bold text-slate-900 dark:text-slate-100">{{ f.title }}</p>
                <p class="mt-1 text-sm text-slate-500 dark:text-slate-400">{{ f.body }}</p>
              </div>
            }
          </div>
        </section>

        <!-- Reviews carousel (auto-moving) -->
        <section class="mt-16">
          <h2 class="text-2xl text-slate-900 dark:text-slate-100 sm:text-3xl">What riders say</h2>
          <div class="marquee mt-6">
            <div class="marquee-track flex gap-4 py-2">
              @for (r of reviewsLoop; track $index) {
                <figure class="card w-80 shrink-0 p-5">
                  <div class="text-accent-400" aria-hidden="true">★★★★★</div>
                  <blockquote class="mt-2 text-sm leading-relaxed text-slate-600 dark:text-slate-300">“{{ r.text }}”</blockquote>
                  <figcaption class="mt-4 flex items-center gap-3">
                    <span class="grid h-9 w-9 place-items-center rounded-full bg-brand-100 text-sm font-bold text-brand-700 dark:bg-brand-500/20 dark:text-brand-300">{{ r.name.charAt(0) }}</span>
                    <div>
                      <p class="text-sm font-semibold text-slate-900 dark:text-slate-100">{{ r.name }}</p>
                      <p class="text-xs text-slate-400 dark:text-slate-500">{{ r.route }}</p>
                    </div>
                  </figcaption>
                </figure>
              }
            </div>
          </div>
        </section>

        <!-- CTA band -->
        <section class="full-bleed mt-16">
          <div class="mx-auto max-w-6xl px-4">
            <div class="relative overflow-hidden rounded-3xl bg-linear-to-br from-brand-700 to-brand-500 px-6 py-12 text-center text-white sm:px-12 sm:py-16">
              <div class="pointer-events-none absolute -right-10 -top-10 h-48 w-48 rounded-full bg-white/10 blur-2xl"></div>
              <h2 class="text-2xl font-bold sm:text-3xl">Ready when you are.</h2>
              <p class="mx-auto mt-2 max-w-md text-white/80">Pick a route and you'll be holding a ticket in under a minute.</p>
              <button type="button" (click)="scrollTop()" class="btn btn-accent mt-6">Find your trip ↑</button>
            </div>
          </div>
        </section>
      }

      <!-- Results -->
      <div class="space-y-3" [class.mt-2]="!searched()">
        @if (loading()) {
          @for (s of [1, 2, 3]; track s) {
            <div class="card flex animate-pulse items-center justify-between gap-4 p-5">
              <div class="flex items-center gap-4">
                <div class="h-12 w-12 rounded-2xl bg-slate-100"></div>
                <div class="space-y-2">
                  <div class="h-4 w-40 rounded bg-slate-100 dark:bg-white/10"></div>
                  <div class="h-3 w-56 rounded bg-slate-100 dark:bg-white/10"></div>
                </div>
              </div>
              <div class="h-10 w-28 rounded-full bg-slate-100"></div>
            </div>
          }
        } @else {
          @if (searched() && results().length === 0) {
            <div class="card flex flex-col items-center gap-2 p-12 text-center">
              <ng-icon name="phosphorMapTrifold" class="text-4xl text-slate-300 dark:text-slate-600" />
              <p class="text-lg font-bold text-slate-900 dark:text-slate-100">No trips on that route yet</p>
              <p class="text-sm text-slate-500 dark:text-slate-400">Try a different date, or clear the extra filters.</p>
            </div>
          }
          @if (results().length > 0) {
            <h2 class="text-xl text-slate-900 dark:text-slate-100">{{ results().length }} trips found</h2>
          }
          @for (trip of results(); track trip.id) {
            <article class="card flex flex-col gap-4 p-5 transition hover:-translate-y-0.5 hover:shadow-glow sm:flex-row sm:items-center sm:justify-between">
              <div class="flex items-center gap-4">
                <div class="grid h-12 w-12 shrink-0 place-items-center rounded-2xl bg-linear-to-br from-brand-500 to-brand-700 text-xl text-white"><ng-icon name="phosphorBus" /></div>
                <div>
                  <p class="flex items-center gap-2 text-base font-bold text-slate-900 dark:text-slate-100">
                    {{ trip.origin }} <span class="text-brand-500">→</span> {{ trip.destination }}
                  </p>
                  <p class="text-sm text-slate-500 dark:text-slate-400">
                    {{ trip.departureUtc | date: 'EEE, MMM d, HH:mm' }} · arrives {{ trip.arrivalUtc | date: 'HH:mm' }}
                  </p>
                  <div class="mt-1.5">
                    @if (trip.availableSeats === 0) {
                      <span class="badge badge-muted">Sold out</span>
                    } @else if (trip.availableSeats <= 5) {
                      <span class="badge badge-accent">Only {{ trip.availableSeats }} seats left</span>
                    } @else {
                      <span class="badge badge-brand">{{ trip.availableSeats }} seats available</span>
                    }
                  </div>
                </div>
              </div>
              <div class="flex items-center justify-between gap-5 sm:flex-col sm:items-end">
                <div class="text-right">
                  <div class="text-2xl font-bold text-slate-900 dark:text-slate-100">
                    {{ trip.price | number: '1.0-0' }} <span class="text-sm font-semibold text-slate-500 dark:text-slate-400">{{ trip.currency }}</span>
                  </div>
                  <div class="text-xs text-slate-400 dark:text-slate-500">per seat</div>
                </div>
                <button type="button" [disabled]="trip.availableSeats === 0" (click)="book(trip)" class="btn btn-primary">
                  Select seats
                </button>
              </div>
            </article>
          }
        }
      </div>
    </div>
  `,
})
export class SearchComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly flow = inject(BookingFlow);
  private readonly router = inject(Router);
  private readonly seo = inject(SeoService);
  private readonly seoApi = inject(SeoApiService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly loading = signal(false);
  protected readonly searched = signal(false);
  protected readonly results = signal<TripSummary[]>([]);
  protected readonly companies = signal<PublicCompany[]>([]);
  /** Top routes for the crawlable "Popular routes" grid. */
  protected readonly seoRoutes = signal<SeoRoute[]>([]);

  protected readonly popularRoutes: Route[] = [
    { from: 'Damascus', to: 'Latakia' },
    { from: 'Damascus', to: 'Aleppo' },
    { from: 'Homs', to: 'Damascus' },
    { from: 'Aleppo', to: 'Latakia' },
  ];

  protected readonly features = [
    { icon: 'phosphorSeat', title: 'Pick your seat', body: 'A live seat map for every bus, so you pick exactly where you sit.' },
    { icon: 'phosphorLockKey', title: 'Secure payment', body: 'Pay through a trusted gateway. We never store your card.' },
    { icon: 'phosphorTicket', title: 'Instant ticket', body: 'Get a QR ticket the moment your booking is confirmed.' },
  ];

  protected readonly reviews = [
    { name: 'Rana K.', route: 'Damascus → Aleppo', text: 'Booked in a minute and picked my exact seat. So easy.' },
    { name: 'Omar H.', route: 'Homs → Damascus', text: 'Loved seeing every company in one place and grabbing the best price.' },
    { name: 'Lina S.', route: 'Latakia → Damascus', text: 'The QR ticket worked perfectly at boarding. No paper needed.' },
    { name: 'Yusuf A.', route: 'Aleppo → Latakia', text: 'I had to cancel and the refund was quick and painless.' },
    { name: 'Maya D.', route: 'Damascus → Latakia', text: 'Finally a clean, modern way to book buses. Highly recommend.' },
  ];
  // Duplicated so the marquee can loop seamlessly (the track scrolls by exactly one copy).
  protected readonly reviewsLoop = [...this.reviews, ...this.reviews];

  protected readonly form = this.fb.nonNullable.group({
    origin: ['', [Validators.required, Validators.maxLength(120)]],
    destination: ['', [Validators.required, Validators.maxLength(120)]],
    date: [this.today(), [Validators.required]],
    companyId: [''],
    maxPrice: [null as number | null],
    departAfter: [''],
  });

  ngOnInit(): void {
    // Render the popular-routes grid from seed data first so it's present in the prerendered HTML.
    this.seoRoutes.set(this.fallbackRoutes());

    // Browser-only network work (companies filter + live popular routes). These calls MUST NOT run
    // during prerender — a relative /api URL can't resolve on the server and the failed request
    // throws an uncaught error that aborts the render.
    if (this.isBrowser) {
      // Populate the company filter; failure is non-fatal (the dropdown just stays "Any company").
      this.api.companies().subscribe({ next: (c) => this.companies.set(c), error: () => undefined });
      // Top ~8 routes for the crawlable "Popular routes" grid.
      this.seoApi.routes().subscribe({
        next: (routes) => this.seoRoutes.set(routes.slice(0, 8)),
        error: () => undefined, // keep the seed-rendered grid
      });
    }

    // Home-page SEO metadata + structured data (baked into the prerendered HTML).
    this.seo.set({
      title: 'TPX Travel — Book Bus Tickets Across Syria Online',
      description:
        'Book intercity bus tickets across Syria with TPX Travel. Compare every operator, pick your seat on a live map, and pay securely for an instant QR ticket. Damascus, Aleppo, Homs & more.',
      path: '/',
      type: 'website',
    });
    this.seo.setJsonLd('organization', {
      '@context': 'https://schema.org',
      '@type': 'Organization',
      name: 'TPX Travel',
      url: this.seo.abs('/'),
      logo: this.seo.abs('/og-default.png'),
      description: 'Online bus ticket booking across Syria.',
    });
    this.seo.setJsonLd('website', {
      '@context': 'https://schema.org',
      '@type': 'WebSite',
      name: 'TPX Travel',
      url: this.seo.abs('/'),
      potentialAction: {
        '@type': 'SearchAction',
        target: {
          '@type': 'EntryPoint',
          urlTemplate: `${this.seo.abs('/')}?origin={origin}&destination={destination}`,
        },
        'query-input': 'required name=origin',
      },
    });
  }

  private fallbackRoutes(): SeoRoute[] {
    return seedRouteSlugs()
      .slice(0, 8)
      .map((slug) => {
        const [origin, destination] = slug.split('-to-').map((s) =>
          s
            .split('-')
            .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
            .join(' '),
        );
        return { origin, destination, slug, tripCount: 0, minPrice: 0, currency: 'SYP' };
      });
  }

  protected quickRoute(route: Route): void {
    this.form.patchValue({ origin: route.from, destination: route.to });
    this.search();
  }

  protected scrollTop(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  protected search(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { origin, destination, date, companyId, maxPrice, departAfter } = this.form.getRawValue();
    this.loading.set(true);
    this.api
      .searchTrips(origin.trim(), destination.trim(), date, {
        companyId: companyId || undefined,
        maxPrice: maxPrice != null ? Number(maxPrice) : undefined,
        departAfter: departAfter || undefined,
      })
      .subscribe({
        next: (trips) => {
          this.results.set(trips);
          this.searched.set(true);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  protected book(trip: TripSummary): void {
    // Hold the chosen trip for the booking page, then route there — the guard bounces an
    // anonymous user to login and back, and the in-memory selection survives that round-trip.
    this.flow.select(trip);
    if (!this.auth.isAuthenticated()) {
      this.router.navigate(['/login'], { queryParams: { returnUrl: `/book/${trip.id}` } });
      return;
    }
    this.router.navigate(['/book', trip.id]);
  }

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }
}
