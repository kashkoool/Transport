import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ScheduleTripRequest,
  TripStopInput,
  UpdateTripRequest,
  VendorApiService,
} from '../../core/api/vendor-api.service';
import { ApiService } from '../../core/api/api.service';
import { ToastService } from '../../core/toast/toast.service';
import { Bus, VendorTrip } from '../../core/models';
import { VendorNavComponent } from './vendor-nav';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-vendor-trips',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, VendorNavComponent, TranslatePipe],
  template: `
    <app-vendor-nav />
    <h1 class="mb-6 text-2xl font-bold text-slate-900">{{ 'vendor.trips.title' | t }}</h1>

    <div class="grid gap-6 lg:grid-cols-3">
      <section class="lg:col-span-2">
        @if (loading()) {
          <p class="text-slate-500">{{ 'vendor.common.loading' | t }}</p>
        } @else if (trips().length === 0) {
          <p class="rounded-lg bg-slate-100 p-4 text-slate-600">{{ 'vendor.trips.empty' | t }}</p>
        } @else {
          <div class="space-y-3">
            @for (t of trips(); track t.id) {
              <div class="rounded-xl border border-slate-200 bg-white p-4">
                <div class="flex items-center justify-between">
                  <div>
                    <p class="font-semibold text-slate-900">{{ t.origin }} → {{ t.destination }}</p>
                    <p class="text-sm text-slate-500">
                      {{ t.departureUtc | date: 'EEE, MMM d • HH:mm' }} · {{ t.seatCount }} {{ 'vendor.trips.seats' | t }} ·
                      {{ t.price | number: '1.0-0' }} {{ t.currency }}
                    </p>
                  </div>
                  <span
                    class="rounded-full px-3 py-1 text-xs font-semibold"
                    [class.bg-emerald-100]="t.status === 'Scheduled'"
                    [class.text-emerald-700]="t.status === 'Scheduled'"
                    [class.bg-sky-100]="t.status === 'InProgress'"
                    [class.text-sky-700]="t.status === 'InProgress'"
                    [class.bg-slate-200]="t.status === 'Completed' || t.status === 'Cancelled'"
                    [class.text-slate-600]="t.status === 'Completed' || t.status === 'Cancelled'"
                    >{{ statusLabel(t.status) }}</span
                  >
                </div>

                <div class="mt-3 flex flex-wrap items-center gap-2 border-t border-slate-100 pt-3">
                  @if (t.status === 'Scheduled') {
                    <button type="button" [disabled]="busy() === t.id" (click)="start(t)" class="rounded-md bg-sky-50 px-3 py-1.5 text-sm font-medium text-sky-700 hover:bg-sky-100 disabled:opacity-50">{{ 'vendor.trips.start' | t }}</button>
                    <button type="button" (click)="edit(t)" class="text-sm font-medium text-brand-600 hover:text-brand-700">{{ 'vendor.common.edit' | t }}</button>
                    <button type="button" (click)="openStops(t)" class="text-sm font-medium text-slate-600 hover:text-slate-800">{{ 'vendor.trips.stops' | t }}</button>
                    <button type="button" [disabled]="busy() === t.id" (click)="cancel(t)" class="text-sm font-medium text-rose-600 hover:text-rose-700">{{ 'vendor.trips.cancel' | t }}</button>
                    @if (confirmingDelete() === t.id) {
                      <button type="button" [disabled]="busy() === t.id" (click)="remove(t)" class="text-sm font-medium text-rose-700">{{ 'vendor.common.confirmDelete' | t }}</button>
                      <button type="button" (click)="confirmingDelete.set(null)" class="text-sm text-slate-500">{{ 'vendor.common.keep' | t }}</button>
                    } @else {
                      <button type="button" (click)="confirmingDelete.set(t.id)" class="text-sm text-slate-400 hover:text-rose-600">{{ 'vendor.common.delete' | t }}</button>
                    }
                  } @else if (t.status === 'InProgress') {
                    <button type="button" [disabled]="busy() === t.id" (click)="complete(t)" class="rounded-md bg-emerald-50 px-3 py-1.5 text-sm font-medium text-emerald-700 hover:bg-emerald-100 disabled:opacity-50">{{ 'vendor.trips.markCompleted' | t }}</button>
                    <button type="button" (click)="openStops(t)" class="text-sm font-medium text-slate-600 hover:text-slate-800">{{ 'vendor.trips.stops' | t }}</button>
                  } @else if (t.status === 'Cancelled') {
                    <button type="button" [disabled]="busy() === t.id" (click)="revert(t)" class="rounded-md bg-sky-50 px-3 py-1.5 text-sm font-medium text-sky-700 hover:bg-sky-100 disabled:opacity-50">{{ 'vendor.trips.reactivate' | t }}</button>
                  }
                </div>

                @if (stopsTripId() === t.id) {
                  <form [formGroup]="stopsForm" (ngSubmit)="saveStops(t)" class="mt-3 rounded-lg border border-slate-100 bg-slate-50 p-3">
                    <p class="mb-2 text-sm font-semibold text-slate-700">{{ 'vendor.trips.intermediateStops' | t }}</p>
                    <div formArrayName="stops" class="space-y-2">
                      @for (g of stops.controls; track $index) {
                        <div [formGroupName]="$index" class="flex flex-wrap items-center gap-2">
                          <input type="text" formControlName="name" [placeholder]="'vendor.trips.stopName' | t" class="flex-1 rounded-md border border-slate-300 px-2 py-1 text-sm focus:border-brand-500 focus:outline-none" />
                          <input type="datetime-local" formControlName="arrival" class="rounded-md border border-slate-300 px-2 py-1 text-sm focus:border-brand-500 focus:outline-none" />
                          <button type="button" (click)="removeStop($index)" class="text-sm text-rose-500 hover:text-rose-700">✕</button>
                        </div>
                      }
                    </div>
                    <div class="mt-2 flex gap-2">
                      <button type="button" (click)="addStop()" class="text-sm font-medium text-brand-600 hover:text-brand-700">{{ 'vendor.trips.addStop' | t }}</button>
                      <span class="flex-1"></span>
                      <button type="submit" [disabled]="busy() === t.id" class="rounded-md bg-brand-600 px-3 py-1 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50">{{ 'vendor.trips.saveStops' | t }}</button>
                      <button type="button" (click)="stopsTripId.set(null)" class="text-sm text-slate-500">{{ 'vendor.common.close' | t }}</button>
                    </div>
                  </form>
                }
              </div>
            }
          </div>
          <p class="mt-2 text-xs text-slate-400">{{ 'vendor.trips.count' | t: { n: total() } }}</p>
        }
      </section>

      <section class="rounded-xl border border-slate-200 bg-white p-4">
        <h2 class="mb-3 font-semibold text-slate-900">{{ (editingId() ? 'vendor.trips.editTrip' : 'vendor.trips.scheduleTrip') | t }}</h2>
        @if (buses().length === 0 && !loading()) {
          <p class="text-sm text-slate-500">{{ 'vendor.trips.addBusFirst' | t }}</p>
        } @else {
          <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3">
            <div>
              <label for="busId" class="mb-1 block text-sm font-medium text-slate-700">{{ 'vendor.trips.bus' | t }}</label>
              <select id="busId" formControlName="busId" [attr.disabled]="editingId() ? '' : null" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500">
                <option value="">{{ 'vendor.common.select' | t }}</option>
                @for (b of buses(); track b.id) {
                  <option [value]="b.id">{{ 'vendor.trips.busOption' | t: { number: b.busNumber, seats: b.seatCount, type: b.type } }}</option>
                }
              </select>
            </div>
            <div class="grid grid-cols-2 gap-2">
              <input type="text" formControlName="origin" [placeholder]="'common.from' | t" class="rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
              <input type="text" formControlName="destination" [placeholder]="'common.to' | t" class="rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
            </div>
            <div>
              <label for="departure" class="mb-1 block text-sm font-medium text-slate-700">{{ 'vendor.trips.departure' | t }}</label>
              <input id="departure" type="datetime-local" formControlName="departure" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
            </div>
            <div>
              <label for="arrival" class="mb-1 block text-sm font-medium text-slate-700">{{ 'vendor.trips.arrival' | t }}</label>
              <input id="arrival" type="datetime-local" formControlName="arrival" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
            </div>
            <div class="grid grid-cols-2 gap-2">
              <input type="number" min="0" step="1000" formControlName="price" [placeholder]="'vendor.trips.price' | t" class="rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
              <input type="text" maxlength="3" formControlName="currency" placeholder="SYP" class="rounded-md border border-slate-300 px-3 py-2 uppercase focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
            </div>
            <button type="submit" [disabled]="submitting()" class="w-full rounded-md bg-brand-600 px-4 py-2 font-medium text-white hover:bg-brand-700 disabled:opacity-50">
              {{ (submitting() ? 'vendor.common.saving' : editingId() ? 'vendor.common.saveChanges' : 'vendor.trips.scheduleTripBtn') | t }}
            </button>
            @if (editingId()) {
              <button type="button" (click)="cancelEdit()" class="w-full text-sm text-slate-500 hover:text-slate-700">{{ 'vendor.common.cancelEdit' | t }}</button>
            }
          </form>
        }
      </section>
    </div>
  `,
})
export class VendorTripsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(VendorApiService);
  private readonly customerApi = inject(ApiService);
  private readonly toasts = inject(ToastService);
  private readonly i18n = inject(TranslationService);

  /** Translate the known trip statuses; unknown values fall back to the raw string. */
  protected statusLabel(status: string): string {
    switch (status) {
      case 'Scheduled': return this.i18n.t('vendor.trips.status.scheduled');
      case 'InProgress': return this.i18n.t('vendor.trips.status.inProgress');
      case 'Completed': return this.i18n.t('vendor.trips.status.completed');
      case 'Cancelled': return this.i18n.t('vendor.trips.status.cancelled');
      default: return status;
    }
  }

  protected readonly trips = signal<VendorTrip[]>([]);
  protected readonly total = signal(0);
  protected readonly buses = signal<Bus[]>([]);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly busy = signal<string | null>(null);
  protected readonly editingId = signal<string | null>(null);
  protected readonly confirmingDelete = signal<string | null>(null);
  protected readonly stopsTripId = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    busId: ['', [Validators.required]],
    origin: ['', [Validators.required, Validators.maxLength(120)]],
    destination: ['', [Validators.required, Validators.maxLength(120)]],
    departure: ['', [Validators.required]],
    arrival: ['', [Validators.required]],
    price: [50000, [Validators.required, Validators.min(0), Validators.max(9999999.99)]],
    currency: ['SYP', [Validators.required, Validators.minLength(3), Validators.maxLength(3)]],
  });

  protected readonly stops = new FormArray<FormGroup>([]);
  protected readonly stopsForm = this.fb.group({ stops: this.stops });

  ngOnInit(): void {
    this.load();
    this.api.listBuses(1, 100).subscribe({ next: (p) => this.buses.set(p.data) });
  }

  private load(): void {
    this.loading.set(true);
    this.api.listTrips(1, 100).subscribe({
      next: (page) => {
        this.trips.set(page.data);
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
    this.submitting.set(true);
    const editId = this.editingId();
    if (editId) {
      const body: UpdateTripRequest = {
        origin: v.origin.trim(),
        destination: v.destination.trim(),
        departureUtc: new Date(v.departure).toISOString(),
        arrivalUtc: new Date(v.arrival).toISOString(),
        price: v.price,
        currency: v.currency.toUpperCase(),
      };
      this.api.updateTrip(editId, body).subscribe({
        next: () => {
          this.submitting.set(false);
          this.toasts.success(this.i18n.t('vendor.trips.toast.updated'));
          this.cancelEdit();
          this.load();
        },
        error: () => this.submitting.set(false),
      });
    } else {
      const body: ScheduleTripRequest = {
        busId: v.busId,
        origin: v.origin.trim(),
        destination: v.destination.trim(),
        departureUtc: new Date(v.departure).toISOString(),
        arrivalUtc: new Date(v.arrival).toISOString(),
        price: v.price,
        currency: v.currency.toUpperCase(),
      };
      this.api.scheduleTrip(body).subscribe({
        next: () => {
          this.submitting.set(false);
          this.toasts.success(this.i18n.t('vendor.trips.toast.scheduled'));
          this.cancelEdit();
          this.load();
        },
        error: () => this.submitting.set(false),
      });
    }
  }

  protected edit(trip: VendorTrip): void {
    this.editingId.set(trip.id);
    this.form.reset({
      busId: trip.busId,
      origin: trip.origin,
      destination: trip.destination,
      departure: toLocalInput(trip.departureUtc),
      arrival: toLocalInput(trip.arrivalUtc),
      price: trip.price,
      currency: trip.currency,
    });
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
    this.form.reset({ busId: '', origin: '', destination: '', departure: '', arrival: '', price: 50000, currency: 'SYP' });
  }

  protected start(trip: VendorTrip): void {
    this.busy.set(trip.id);
    this.api.startTrip(trip.id).subscribe({
      next: () => { this.busy.set(null); this.toasts.success(this.i18n.t('vendor.trips.toast.started')); this.load(); },
      error: () => this.busy.set(null),
    });
  }

  protected complete(trip: VendorTrip): void {
    this.busy.set(trip.id);
    this.api.completeTrip(trip.id).subscribe({
      next: () => { this.busy.set(null); this.toasts.success(this.i18n.t('vendor.trips.toast.completed')); this.load(); },
      error: () => this.busy.set(null),
    });
  }

  protected revert(trip: VendorTrip): void {
    this.busy.set(trip.id);
    this.api.revertTrip(trip.id).subscribe({
      next: () => { this.busy.set(null); this.toasts.success(this.i18n.t('vendor.trips.toast.reactivated')); this.load(); },
      error: () => this.busy.set(null),
    });
  }

  protected cancel(trip: VendorTrip): void {
    this.busy.set(trip.id);
    this.api.cancelTrip(trip.id).subscribe({
      next: (r) => {
        this.busy.set(null);
        const extra = r.confirmedBookingsAffected > 0 ? this.i18n.t('vendor.trips.toast.cancelledRefund', { n: r.confirmedBookingsAffected }) : '';
        this.toasts.info(this.i18n.t('vendor.trips.toast.cancelled', { holds: r.releasedHolds, pending: r.cancelledPendingBookings, extra }));
        this.load();
      },
      error: () => this.busy.set(null),
    });
  }

  protected remove(trip: VendorTrip): void {
    this.busy.set(trip.id);
    this.api.deleteTrip(trip.id).subscribe({
      next: () => { this.busy.set(null); this.confirmingDelete.set(null); this.toasts.success(this.i18n.t('vendor.trips.toast.deleted')); this.load(); },
      error: () => { this.busy.set(null); this.confirmingDelete.set(null); },
    });
  }

  // ── Stops editor ─────────────────────────────────────────────────────────────────
  protected openStops(trip: VendorTrip): void {
    this.stopsTripId.set(trip.id);
    this.stops.clear();
    this.customerApi.tripStops(trip.id).subscribe({
      next: (existing) => {
        for (const s of existing) this.pushStop(s.name, s.arrivalUtc);
        if (existing.length === 0) this.addStop();
      },
      error: () => this.addStop(),
    });
  }

  protected addStop(): void {
    this.pushStop('', null);
  }

  protected removeStop(index: number): void {
    this.stops.removeAt(index);
  }

  protected saveStops(trip: VendorTrip): void {
    const rows = this.stops.getRawValue() as { name: string; arrival: string }[];
    const stops: TripStopInput[] = rows
      .filter((r) => r.name.trim())
      .map((r) => ({
        name: r.name.trim(),
        arrivalUtc: r.arrival ? new Date(r.arrival).toISOString() : null,
        departureUtc: null,
      }));
    this.busy.set(trip.id);
    this.api.setTripStops(trip.id, stops).subscribe({
      next: () => { this.busy.set(null); this.stopsTripId.set(null); this.toasts.success(this.i18n.t('vendor.trips.toast.stopsSaved')); },
      error: () => this.busy.set(null),
    });
  }

  private pushStop(name: string, arrivalUtc: string | null): void {
    this.stops.push(
      this.fb.nonNullable.group({
        name: [name, [Validators.required, Validators.maxLength(120)]],
        arrival: [arrivalUtc ? toLocalInput(arrivalUtc) : ''],
      }),
    );
  }
}

/** Convert a UTC ISO instant to the value a <input type="datetime-local"> expects (local wall-clock). */
function toLocalInput(utc: string): string {
  const d = new Date(utc);
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
