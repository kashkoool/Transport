import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { BookingFlow } from '../../core/booking-flow';
import { TripSummary } from '../../core/models';

@Component({
  selector: 'app-search',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe],
  template: `
    <h1 class="mb-6 text-2xl font-bold text-slate-900">Find your trip</h1>

    <form
      [formGroup]="form"
      (ngSubmit)="search()"
      class="grid gap-4 rounded-xl border border-slate-200 bg-white p-4 sm:grid-cols-4"
    >
      <div class="sm:col-span-1">
        <label for="origin" class="mb-1 block text-sm font-medium text-slate-700">From</label>
        <input
          id="origin"
          type="text"
          formControlName="origin"
          placeholder="Damascus"
          class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
        />
      </div>
      <div class="sm:col-span-1">
        <label for="destination" class="mb-1 block text-sm font-medium text-slate-700">To</label>
        <input
          id="destination"
          type="text"
          formControlName="destination"
          placeholder="Latakia"
          class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
        />
      </div>
      <div class="sm:col-span-1">
        <label for="date" class="mb-1 block text-sm font-medium text-slate-700">Date</label>
        <input
          id="date"
          type="date"
          formControlName="date"
          class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
        />
      </div>
      <div class="flex items-end">
        <button
          type="submit"
          [disabled]="loading()"
          class="w-full rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
        >
          {{ loading() ? 'Searching…' : 'Search' }}
        </button>
      </div>
    </form>

    <div class="mt-6 space-y-3">
      @if (searched() && results().length === 0 && !loading()) {
        <p class="rounded-lg bg-slate-100 p-4 text-center text-slate-600">
          No trips found for that route and date.
        </p>
      }
      @for (trip of results(); track trip.id) {
        <div
          class="flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-4 sm:flex-row sm:items-center sm:justify-between"
        >
          <div>
            <p class="font-semibold text-slate-900">{{ trip.origin }} → {{ trip.destination }}</p>
            <p class="text-sm text-slate-500">
              {{ trip.departureUtc | date: 'EEE, MMM d • HH:mm' }} —
              arrives {{ trip.arrivalUtc | date: 'HH:mm' }}
            </p>
            <p class="text-sm" [class.text-rose-600]="trip.availableSeats === 0" [class.text-emerald-600]="trip.availableSeats > 0">
              {{ trip.availableSeats }} of {{ trip.seatCount }} seats available
            </p>
          </div>
          <div class="flex items-center gap-4">
            <span class="text-lg font-bold text-slate-900">{{ trip.price | number: '1.0-0' }} {{ trip.currency }}</span>
            <button
              type="button"
              [disabled]="trip.availableSeats === 0"
              (click)="book(trip)"
              class="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:cursor-not-allowed disabled:opacity-50"
            >
              Select seats
            </button>
          </div>
        </div>
      }
    </div>
  `,
})
export class SearchComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly flow = inject(BookingFlow);
  private readonly router = inject(Router);

  protected readonly loading = signal(false);
  protected readonly searched = signal(false);
  protected readonly results = signal<TripSummary[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    origin: ['', [Validators.required, Validators.maxLength(120)]],
    destination: ['', [Validators.required, Validators.maxLength(120)]],
    date: [this.today(), [Validators.required]],
  });

  protected search(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { origin, destination, date } = this.form.getRawValue();
    this.loading.set(true);
    this.api.searchTrips(origin.trim(), destination.trim(), date).subscribe({
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
