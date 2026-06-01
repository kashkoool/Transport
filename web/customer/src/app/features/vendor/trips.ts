import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ScheduleTripRequest, VendorApiService } from '../../core/api/vendor-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { Bus, VendorTrip } from '../../core/models';
import { VendorNavComponent } from './vendor-nav';

@Component({
  selector: 'app-vendor-trips',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, VendorNavComponent],
  template: `
    <app-vendor-nav />
    <h1 class="mb-6 text-2xl font-bold text-slate-900">Trips</h1>

    <div class="grid gap-6 lg:grid-cols-3">
      <section class="lg:col-span-2">
        @if (loading()) {
          <p class="text-slate-500">Loading…</p>
        } @else if (trips().length === 0) {
          <p class="rounded-lg bg-slate-100 p-4 text-slate-600">No trips scheduled yet.</p>
        } @else {
          <div class="space-y-3">
            @for (t of trips(); track t.id) {
              <div class="flex items-center justify-between rounded-xl border border-slate-200 bg-white p-4">
                <div>
                  <p class="font-semibold text-slate-900">{{ t.origin }} → {{ t.destination }}</p>
                  <p class="text-sm text-slate-500">
                    {{ t.departureUtc | date: 'EEE, MMM d • HH:mm' }} · {{ t.seatCount }} seats ·
                    {{ t.price | number: '1.0-0' }} {{ t.currency }}
                  </p>
                </div>
                <div class="flex items-center gap-3">
                  <span
                    class="rounded-full px-3 py-1 text-xs font-semibold"
                    [class.bg-emerald-100]="t.status === 'Scheduled'"
                    [class.text-emerald-700]="t.status === 'Scheduled'"
                    [class.bg-slate-200]="t.status !== 'Scheduled'"
                    [class.text-slate-600]="t.status !== 'Scheduled'"
                    >{{ t.status }}</span
                  >
                  @if (t.status === 'Scheduled') {
                    <button
                      type="button"
                      [disabled]="cancelling() === t.id"
                      (click)="cancel(t)"
                      class="rounded-md bg-rose-50 px-3 py-1.5 text-sm font-medium text-rose-700 hover:bg-rose-100 disabled:opacity-50"
                    >
                      {{ cancelling() === t.id ? 'Cancelling…' : 'Cancel' }}
                    </button>
                  }
                </div>
              </div>
            }
          </div>
          <p class="mt-2 text-xs text-slate-400">{{ total() }} trip(s)</p>
        }
      </section>

      <section class="rounded-xl border border-slate-200 bg-white p-4">
        <h2 class="mb-3 font-semibold text-slate-900">Schedule a trip</h2>
        @if (buses().length === 0 && !loading()) {
          <p class="text-sm text-slate-500">Add a bus to your fleet first.</p>
        } @else {
          <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3">
            <div>
              <label for="busId" class="mb-1 block text-sm font-medium text-slate-700">Bus</label>
              <select id="busId" formControlName="busId" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500">
                <option value="">Select…</option>
                @for (b of buses(); track b.id) {
                  <option [value]="b.id">{{ b.busNumber }} ({{ b.seatCount }} seats, {{ b.type }})</option>
                }
              </select>
            </div>
            <div class="grid grid-cols-2 gap-2">
              <input type="text" formControlName="origin" placeholder="From" class="rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
              <input type="text" formControlName="destination" placeholder="To" class="rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
            </div>
            <div>
              <label for="departure" class="mb-1 block text-sm font-medium text-slate-700">Departure</label>
              <input id="departure" type="datetime-local" formControlName="departure" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
            </div>
            <div>
              <label for="arrival" class="mb-1 block text-sm font-medium text-slate-700">Arrival</label>
              <input id="arrival" type="datetime-local" formControlName="arrival" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
            </div>
            <div class="grid grid-cols-2 gap-2">
              <input type="number" min="0" step="1000" formControlName="price" placeholder="Price" class="rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
              <input type="text" maxlength="3" formControlName="currency" placeholder="SYP" class="rounded-md border border-slate-300 px-3 py-2 uppercase focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
            </div>
            <button type="submit" [disabled]="submitting()" class="w-full rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-50">
              {{ submitting() ? 'Scheduling…' : 'Schedule trip' }}
            </button>
          </form>
        }
      </section>
    </div>
  `,
})
export class VendorTripsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(VendorApiService);
  private readonly toasts = inject(ToastService);

  protected readonly trips = signal<VendorTrip[]>([]);
  protected readonly total = signal(0);
  protected readonly buses = signal<Bus[]>([]);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly cancelling = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    busId: ['', [Validators.required]],
    origin: ['', [Validators.required, Validators.maxLength(120)]],
    destination: ['', [Validators.required, Validators.maxLength(120)]],
    departure: ['', [Validators.required]],
    arrival: ['', [Validators.required]],
    price: [50000, [Validators.required, Validators.min(0), Validators.max(9999999.99)]],
    currency: ['SYP', [Validators.required, Validators.minLength(3), Validators.maxLength(3)]],
  });

  ngOnInit(): void {
    this.load();
    this.api.listBuses(1, 100).subscribe({ next: (p) => this.buses.set(p.items) });
  }

  private load(): void {
    this.loading.set(true);
    this.api.listTrips().subscribe({
      next: (page) => {
        this.trips.set(page.items);
        this.total.set(page.total);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body: ScheduleTripRequest = {
      busId: v.busId,
      origin: v.origin.trim(),
      destination: v.destination.trim(),
      // datetime-local is local wall-clock; convert to a UTC instant for the API.
      departureUtc: new Date(v.departure).toISOString(),
      arrivalUtc: new Date(v.arrival).toISOString(),
      price: v.price,
      currency: v.currency.toUpperCase(),
    };
    this.submitting.set(true);
    this.api.scheduleTrip(body).subscribe({
      next: () => {
        this.submitting.set(false);
        this.toasts.success('Trip scheduled.');
        this.form.patchValue({ origin: '', destination: '', departure: '', arrival: '' });
        this.load();
      },
      error: () => this.submitting.set(false),
    });
  }

  protected cancel(trip: VendorTrip): void {
    this.cancelling.set(trip.id);
    this.api.cancelTrip(trip.id).subscribe({
      next: (r) => {
        this.cancelling.set(null);
        const extra = r.confirmedBookingsAffected > 0
          ? ` ${r.confirmedBookingsAffected} paid booking(s) need a refund.`
          : '';
        this.toasts.info(
          `Trip cancelled — released ${r.releasedHolds} hold(s), cancelled ${r.cancelledPendingBookings} pending.${extra}`,
        );
        this.load();
      },
      error: () => this.cancelling.set(null),
    });
  }
}
