import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { BookingFlow } from '../../core/booking-flow';
import { PublicCompany, TripSummary } from '../../core/models';

interface Route {
  from: string;
  to: string;
}

@Component({
  selector: 'app-search',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe],
  template: `
    <!-- Hero + search -->
    <section
      class="relative overflow-hidden rounded-3xl bg-linear-to-br from-brand-700 via-brand-600 to-cyan-500 px-6 pb-28 pt-12 text-white sm:px-12 sm:pt-16"
    >
      <div class="pointer-events-none absolute -right-16 -top-20 h-64 w-64 rounded-full bg-white/10 blur-2xl"></div>
      <div class="pointer-events-none absolute -bottom-12 left-1/4 h-52 w-52 rounded-full bg-cyan-200/20 blur-2xl"></div>
      <div class="relative max-w-2xl">
        <span class="badge bg-white/15 text-white ring-1 ring-white/20">🚌 Syria's bus network, in one place</span>
        <h1 class="mt-4 text-4xl font-extrabold leading-[1.1] sm:text-5xl">Travel Syria,<br />the easy way.</h1>
        <p class="mt-4 max-w-lg text-base text-white/80">
          Compare departures from every company, pick your exact seat, and pay securely — in a couple of taps.
        </p>
      </div>
    </section>

    <!-- Search card overlapping the hero -->
    <form
      [formGroup]="form"
      (ngSubmit)="search()"
      class="card relative z-10 -mt-16 p-5 sm:p-6"
    >
      <div class="grid gap-4 sm:grid-cols-[1fr_1fr_1fr_auto]">
        <div>
          <label for="origin" class="label">From</label>
          <input id="origin" type="text" formControlName="origin" placeholder="Damascus" class="input" />
        </div>
        <div>
          <label for="destination" class="label">To</label>
          <input id="destination" type="text" formControlName="destination" placeholder="Latakia" class="input" />
        </div>
        <div>
          <label for="date" class="label">Date</label>
          <input id="date" type="date" formControlName="date" class="input" />
        </div>
        <div class="flex items-end">
          <button type="submit" [disabled]="loading()" class="btn btn-accent w-full px-6 sm:w-auto">
            @if (loading()) {
              <span class="h-4 w-4 animate-spin rounded-full border-2 border-white/40 border-t-white"></span>
              Searching…
            } @else {
              <span>🔍</span> Search
            }
          </button>
        </div>
      </div>

      <details class="group mt-3">
        <summary class="cursor-pointer select-none text-sm font-medium text-brand-700 hover:text-brand-800">
          More filters
        </summary>
        <div class="mt-3 grid gap-4 sm:grid-cols-3">
          <div>
            <label for="company" class="label">Company</label>
            <select id="company" formControlName="companyId" class="input">
              <option value="">Any company</option>
              @for (c of companies(); track c.id) {
                <option [value]="c.id">{{ c.name }}</option>
              }
            </select>
          </div>
          <div>
            <label for="maxPrice" class="label">Max price</label>
            <input id="maxPrice" type="number" min="0" step="1000" formControlName="maxPrice" placeholder="Any" class="input" />
          </div>
          <div>
            <label for="departAfter" class="label">Depart after</label>
            <input id="departAfter" type="time" formControlName="departAfter" class="input" />
          </div>
        </div>
      </details>
    </form>

    <!-- Pre-search: popular routes + value props -->
    @if (!searched()) {
      <div class="mt-8">
        <h2 class="text-sm font-semibold uppercase tracking-wide text-slate-500">Popular routes</h2>
        <div class="mt-3 flex flex-wrap gap-2">
          @for (r of popularRoutes; track r.from + r.to) {
            <button type="button" (click)="quickRoute(r)" class="btn btn-ghost gap-1.5 px-3 py-1.5">
              <span class="font-semibold text-slate-900">{{ r.from }}</span>
              <span class="text-brand-600">→</span>
              <span class="font-semibold text-slate-900">{{ r.to }}</span>
            </button>
          }
        </div>

        <div class="mt-8 grid gap-4 sm:grid-cols-3">
          @for (f of features; track f.title) {
            <div class="card flex items-start gap-3 p-5">
              <span class="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-brand-50 text-xl">{{ f.icon }}</span>
              <div>
                <p class="font-semibold text-slate-900">{{ f.title }}</p>
                <p class="mt-0.5 text-sm text-slate-500">{{ f.body }}</p>
              </div>
            </div>
          }
        </div>
      </div>
    }

    <!-- Results -->
    <div class="mt-8 space-y-3">
      @if (loading()) {
        @for (s of [1, 2, 3]; track s) {
          <div class="card flex animate-pulse items-center justify-between gap-4 p-5">
            <div class="flex items-center gap-4">
              <div class="h-12 w-12 rounded-xl bg-slate-100"></div>
              <div class="space-y-2">
                <div class="h-4 w-40 rounded bg-slate-100"></div>
                <div class="h-3 w-56 rounded bg-slate-100"></div>
              </div>
            </div>
            <div class="h-9 w-28 rounded-xl bg-slate-100"></div>
          </div>
        }
      } @else {
        @if (searched() && results().length === 0) {
          <div class="card flex flex-col items-center gap-2 p-10 text-center">
            <span class="text-4xl">🗺️</span>
            <p class="font-semibold text-slate-900">No trips on that route yet</p>
            <p class="text-sm text-slate-500">Try a different date, or clear the extra filters.</p>
          </div>
        }
        @if (results().length > 0) {
          <div class="flex items-center justify-between">
            <h2 class="text-lg font-bold text-slate-900">{{ results().length }} trips found</h2>
          </div>
        }
        @for (trip of results(); track trip.id) {
          <article class="card flex flex-col gap-4 p-5 transition hover:shadow-lift sm:flex-row sm:items-center sm:justify-between">
            <div class="flex items-center gap-4">
              <div class="grid h-12 w-12 shrink-0 place-items-center rounded-xl bg-brand-50 text-xl">🚌</div>
              <div>
                <p class="flex items-center gap-2 text-base font-bold text-slate-900">
                  {{ trip.origin }} <span class="text-brand-500">→</span> {{ trip.destination }}
                </p>
                <p class="text-sm text-slate-500">
                  {{ trip.departureUtc | date: 'EEE, MMM d • HH:mm' }} — arrives {{ trip.arrivalUtc | date: 'HH:mm' }}
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
                <div class="text-xl font-extrabold text-slate-900">
                  {{ trip.price | number: '1.0-0' }} <span class="text-sm font-semibold text-slate-500">{{ trip.currency }}</span>
                </div>
                <div class="text-xs text-slate-400">per seat</div>
              </div>
              <button
                type="button"
                [disabled]="trip.availableSeats === 0"
                (click)="book(trip)"
                class="btn btn-primary"
              >
                Select seats
              </button>
            </div>
          </article>
        }
      }
    </div>
  `,
})
export class SearchComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly flow = inject(BookingFlow);
  private readonly router = inject(Router);

  protected readonly loading = signal(false);
  protected readonly searched = signal(false);
  protected readonly results = signal<TripSummary[]>([]);
  protected readonly companies = signal<PublicCompany[]>([]);

  protected readonly popularRoutes: Route[] = [
    { from: 'Damascus', to: 'Latakia' },
    { from: 'Damascus', to: 'Aleppo' },
    { from: 'Homs', to: 'Damascus' },
    { from: 'Aleppo', to: 'Latakia' },
  ];

  protected readonly features = [
    { icon: '💺', title: 'Pick your seat', body: 'A live seat map for every bus — choose exactly where you sit.' },
    { icon: '🔒', title: 'Secure payment', body: 'Pay through a trusted gateway. We never store your card.' },
    { icon: '🎫', title: 'Instant ticket', body: 'Get a QR ticket the moment your booking is confirmed.' },
  ];

  protected readonly form = this.fb.nonNullable.group({
    origin: ['', [Validators.required, Validators.maxLength(120)]],
    destination: ['', [Validators.required, Validators.maxLength(120)]],
    date: [this.today(), [Validators.required]],
    companyId: [''],
    maxPrice: [null as number | null],
    departAfter: [''],
  });

  ngOnInit(): void {
    // Populate the company filter; failure is non-fatal (the dropdown just stays "Any company").
    this.api.companies().subscribe({ next: (c) => this.companies.set(c), error: () => undefined });
  }

  protected quickRoute(route: Route): void {
    this.form.patchValue({ origin: route.from, destination: route.to });
    this.search();
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
