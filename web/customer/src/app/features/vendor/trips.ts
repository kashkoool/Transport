import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  phosphorPath,
  phosphorPlay,
  phosphorPencilSimple,
  phosphorMapPinLine,
  phosphorProhibit,
  phosphorCheckCircle,
  phosphorArrowsClockwise,
  phosphorTrash,
  phosphorCheck,
  phosphorPlus,
  phosphorX,
  phosphorFloppyDisk,
} from '@ng-icons/phosphor-icons/regular';
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
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, VendorNavComponent, NgIcon, TranslatePipe],
  providers: [
    provideIcons({
      phosphorPath,
      phosphorPlay,
      phosphorPencilSimple,
      phosphorMapPinLine,
      phosphorProhibit,
      phosphorCheckCircle,
      phosphorArrowsClockwise,
      phosphorTrash,
      phosphorCheck,
      phosphorPlus,
      phosphorX,
      phosphorFloppyDisk,
    }),
  ],
  template: `
    <div class="lg:grid lg:grid-cols-[15rem_1fr] lg:items-start lg:gap-8">
      <app-vendor-nav />

      <div class="min-w-0">
        <h1 class="animate-in mb-6 font-display text-2xl font-bold text-ink dark:text-white">{{ 'vendor.trips.title' | t }}</h1>

        <div class="grid gap-6 lg:grid-cols-3">
          <section class="lg:col-span-2">
            <div class="mb-3 flex items-center justify-between gap-3">
              <p class="text-sm text-slate-500 dark:text-slate-400">{{ 'vendor.trips.count' | t: { n: total() } }}</p>
            </div>

            @if (loading()) {
              <p class="text-slate-500 dark:text-slate-400">{{ 'vendor.common.loading' | t }}</p>
            } @else if (trips().length === 0) {
              <div class="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-slate-200 bg-slate-50/60 px-6 py-14 text-center dark:border-white/10 dark:bg-white/2">
                <span class="grid h-12 w-12 place-items-center rounded-full bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300">
                  <ng-icon name="phosphorPath" class="text-2xl" aria-hidden="true" />
                </span>
                <p class="text-sm font-medium text-slate-600 dark:text-slate-300">{{ 'vendor.trips.empty' | t }}</p>
                <a href="#schedule-trip-panel" class="btn btn-primary px-4 py-2 text-sm">
                  <ng-icon name="phosphorPlus" aria-hidden="true" />
                  {{ 'vendor.trips.scheduleTrip' | t }}
                </a>
              </div>
            } @else {
              <div class="stagger-children space-y-3">
                @for (t of trips(); track t.id) {
                  <div class="card p-4">
                    <div class="flex items-center justify-between gap-3">
                      <div class="min-w-0">
                        <p class="truncate font-display font-semibold text-ink dark:text-white">{{ t.origin }} → {{ t.destination }}</p>
                        <p class="mt-0.5 text-sm tabular-nums text-slate-500 dark:text-slate-400">
                          {{ t.departureUtc | date: 'EEE, MMM d • HH:mm' }} · {{ t.seatCount }} {{ 'vendor.trips.seats' | t }} ·
                          {{ t.price | number: '1.0-0' }} {{ t.currency }}
                        </p>
                      </div>
                      <span
                        class="badge shrink-0"
                        [class]="
                          t.status === 'Scheduled' ? 'badge-brand' :
                          t.status === 'InProgress' ? 'badge-accent' :
                          t.status === 'Cancelled' ? 'bg-rose-50 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300' :
                          'badge-muted'
                        "
                        >{{ statusLabel(t.status) }}</span
                      >
                    </div>

                    <div class="mt-3 flex flex-wrap items-center gap-1.5 border-t border-slate-100 pt-3 dark:border-white/10">
                      @if (t.status === 'Scheduled') {
                        <button type="button" [disabled]="busy() === t.id" (click)="start(t)" class="btn btn-soft px-3 py-1.5 text-xs">
                          <ng-icon name="phosphorPlay" aria-hidden="true" />{{ 'vendor.trips.start' | t }}
                        </button>
                        <button type="button" (click)="edit(t)" class="btn btn-ghost px-3 py-1.5 text-xs">
                          <ng-icon name="phosphorPencilSimple" aria-hidden="true" />{{ 'vendor.common.edit' | t }}
                        </button>
                        <button type="button" (click)="openStops(t)" class="btn btn-ghost px-3 py-1.5 text-xs">
                          <ng-icon name="phosphorMapPinLine" aria-hidden="true" />{{ 'vendor.trips.stops' | t }}
                        </button>
                        <button type="button" [disabled]="busy() === t.id" (click)="cancel(t)" class="btn btn-ghost px-3 py-1.5 text-xs text-rose-600 hover:text-rose-700 dark:text-rose-400">
                          <ng-icon name="phosphorProhibit" aria-hidden="true" />{{ 'vendor.trips.cancel' | t }}
                        </button>
                        @if (confirmingDelete() === t.id) {
                          <button type="button" [disabled]="busy() === t.id" (click)="remove(t)" class="btn btn-danger px-3 py-1.5 text-xs">
                            <ng-icon name="phosphorCheck" aria-hidden="true" />{{ 'vendor.common.confirmDelete' | t }}
                          </button>
                          <button type="button" (click)="confirmingDelete.set(null)" class="btn btn-ghost px-3 py-1.5 text-xs">{{ 'vendor.common.keep' | t }}</button>
                        } @else {
                          <button type="button" (click)="confirmingDelete.set(t.id)" [attr.aria-label]="'vendor.common.delete' | t" class="btn btn-ghost ms-auto px-3 py-1.5 text-xs text-slate-400 hover:text-rose-600 dark:text-slate-500">
                            <ng-icon name="phosphorTrash" aria-hidden="true" />
                          </button>
                        }
                      } @else if (t.status === 'InProgress') {
                        <button type="button" [disabled]="busy() === t.id" (click)="complete(t)" class="btn btn-soft px-3 py-1.5 text-xs">
                          <ng-icon name="phosphorCheckCircle" aria-hidden="true" />{{ 'vendor.trips.markCompleted' | t }}
                        </button>
                        <button type="button" (click)="openStops(t)" class="btn btn-ghost px-3 py-1.5 text-xs">
                          <ng-icon name="phosphorMapPinLine" aria-hidden="true" />{{ 'vendor.trips.stops' | t }}
                        </button>
                      } @else if (t.status === 'Cancelled') {
                        <button type="button" [disabled]="busy() === t.id" (click)="revert(t)" class="btn btn-soft px-3 py-1.5 text-xs">
                          <ng-icon name="phosphorArrowsClockwise" aria-hidden="true" />{{ 'vendor.trips.reactivate' | t }}
                        </button>
                      }
                    </div>

                    @if (stopsTripId() === t.id) {
                      <form [formGroup]="stopsForm" (ngSubmit)="saveStops(t)" class="mt-3 rounded-xl border border-slate-100 bg-slate-50 p-3 dark:border-white/10 dark:bg-white/3">
                        <p class="mb-2 text-sm font-semibold text-slate-700 dark:text-slate-200">{{ 'vendor.trips.intermediateStops' | t }}</p>
                        <div formArrayName="stops" class="space-y-2">
                          @for (g of stops.controls; track $index) {
                            <div [formGroupName]="$index" class="flex flex-wrap items-center gap-2">
                              <input type="text" formControlName="name" [placeholder]="'vendor.trips.stopName' | t" class="input flex-1 px-3 py-1.5 text-sm" />
                              <input type="datetime-local" formControlName="arrival" class="input w-auto px-3 py-1.5 text-sm" />
                              <button type="button" (click)="removeStop($index)" [attr.aria-label]="'vendor.common.delete' | t" class="btn btn-ghost px-2 py-1.5 text-rose-500 hover:text-rose-700">
                                <ng-icon name="phosphorX" aria-hidden="true" />
                              </button>
                            </div>
                          }
                        </div>
                        <div class="mt-2 flex items-center gap-2">
                          <button type="button" (click)="addStop()" class="btn btn-ghost px-3 py-1.5 text-xs">
                            <ng-icon name="phosphorPlus" aria-hidden="true" />{{ 'vendor.trips.addStop' | t }}
                          </button>
                          <span class="flex-1"></span>
                          <button type="submit" [disabled]="busy() === t.id" class="btn btn-primary px-3 py-1.5 text-xs">
                            <ng-icon name="phosphorFloppyDisk" aria-hidden="true" />{{ 'vendor.trips.saveStops' | t }}
                          </button>
                          <button type="button" (click)="stopsTripId.set(null)" class="btn btn-ghost px-3 py-1.5 text-xs">{{ 'vendor.common.close' | t }}</button>
                        </div>
                      </form>
                    }
                  </div>
                }
              </div>
            }
          </section>

          <section id="schedule-trip-panel" class="card p-5">
            <h2 class="mb-3 font-display font-semibold text-ink dark:text-white">{{ (editingId() ? 'vendor.trips.editTrip' : 'vendor.trips.scheduleTrip') | t }}</h2>
            @if (buses().length === 0 && !loading()) {
              <p class="text-sm text-slate-500 dark:text-slate-400">{{ 'vendor.trips.addBusFirst' | t }}</p>
            } @else {
              <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3.5">
                <div>
                  <label for="busId" class="label">{{ 'vendor.trips.bus' | t }}</label>
                  <select id="busId" formControlName="busId" [attr.disabled]="editingId() ? '' : null" class="input">
                    <option value="">{{ 'vendor.common.select' | t }}</option>
                    @for (b of buses(); track b.id) {
                      <option [value]="b.id">{{ 'vendor.trips.busOption' | t: { number: b.busNumber, seats: b.seatCount, type: b.type } }}</option>
                    }
                  </select>
                </div>
                <div class="grid grid-cols-2 gap-2.5">
                  <input type="text" formControlName="origin" [placeholder]="'common.from' | t" class="input" />
                  <input type="text" formControlName="destination" [placeholder]="'common.to' | t" class="input" />
                </div>
                <div>
                  <label for="departure" class="label">{{ 'vendor.trips.departure' | t }}</label>
                  <input id="departure" type="datetime-local" formControlName="departure" class="input" />
                </div>
                <div>
                  <label for="arrival" class="label">{{ 'vendor.trips.arrival' | t }}</label>
                  <input id="arrival" type="datetime-local" formControlName="arrival" class="input" />
                </div>
                <div class="grid grid-cols-2 gap-2.5">
                  <input type="number" min="0" step="1000" formControlName="price" [placeholder]="'vendor.trips.price' | t" class="input" />
                  <input type="text" maxlength="3" formControlName="currency" placeholder="SYP" class="input uppercase" />
                </div>
                <button type="submit" [disabled]="submitting()" class="btn btn-primary w-full">
                  <ng-icon [name]="editingId() ? 'phosphorFloppyDisk' : 'phosphorPlus'" aria-hidden="true" />
                  {{ (submitting() ? 'vendor.common.saving' : editingId() ? 'vendor.common.saveChanges' : 'vendor.trips.scheduleTripBtn') | t }}
                </button>
                @if (editingId()) {
                  <button type="button" (click)="cancelEdit()" class="btn btn-ghost w-full">{{ 'vendor.common.cancelEdit' | t }}</button>
                }
              </form>
            }
          </section>
        </div>
      </div>
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
