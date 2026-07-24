import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  phosphorTicket,
  phosphorPlus,
  phosphorX,
  phosphorProhibit,
  phosphorCheck,
} from '@ng-icons/phosphor-icons/regular';
import { CounterBookingRequest, VendorApiService } from '../../core/api/vendor-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { CompanyBooking, VendorTrip } from '../../core/models';
import { VendorNavComponent } from './vendor-nav';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-vendor-desk',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, VendorNavComponent, NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorTicket, phosphorPlus, phosphorX, phosphorProhibit, phosphorCheck })],
  template: `
    <div class="lg:grid lg:grid-cols-[15rem_1fr] lg:items-start lg:gap-8">
      <app-vendor-nav />

      <div class="min-w-0">
        <h1 class="animate-in mb-6 font-display text-2xl font-bold text-ink dark:text-white">{{ 'vendor.desk.title' | t }}</h1>

        <div class="grid gap-6 lg:grid-cols-3">
          <section class="card p-5">
            <h2 class="mb-3 font-display font-semibold text-ink dark:text-white">{{ 'vendor.desk.newBooking' | t }}</h2>
            <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3.5">
              <div>
                <label for="tripId" class="label">{{ 'vendor.desk.trip' | t }}</label>
                <select id="tripId" formControlName="tripId" class="input">
                  <option value="">{{ 'vendor.common.select' | t }}</option>
                  @for (t of trips(); track t.id) {
                    <option [value]="t.id">{{ t.origin }} → {{ t.destination }} · {{ t.departureUtc | date: 'MMM d, HH:mm' }}</option>
                  }
                </select>
              </div>
              <div>
                <label for="customerEmail" class="label">{{ 'vendor.desk.customerEmail' | t }}</label>
                <input id="customerEmail" type="email" formControlName="customerEmail" class="input" />
                <p class="mt-1 text-xs text-slate-400 dark:text-slate-500">{{ 'vendor.desk.emailHint' | t }}</p>
              </div>

              <div formArrayName="passengers" class="space-y-2">
                <p class="label mb-1">{{ 'vendor.desk.passengers' | t }}</p>
                @for (g of passengers.controls; track $index) {
                  <div [formGroupName]="$index" class="flex items-center gap-2">
                    <input type="text" formControlName="firstName" [placeholder]="'vendor.desk.firstName' | t" class="input w-1/3 px-3 py-1.5 text-sm" />
                    <input type="text" formControlName="lastName" [placeholder]="'vendor.desk.lastName' | t" class="input w-1/3 px-3 py-1.5 text-sm" />
                    <input type="number" min="1" formControlName="seatNumber" [placeholder]="'vendor.desk.seat' | t" class="input w-1/4 px-3 py-1.5 text-sm" />
                    @if (passengers.length > 1) {
                      <button type="button" (click)="removePassenger($index)" [attr.aria-label]="'vendor.common.delete' | t" class="btn btn-ghost px-2 py-1.5 text-rose-500 hover:text-rose-700">
                        <ng-icon name="phosphorX" aria-hidden="true" />
                      </button>
                    }
                  </div>
                }
                <button type="button" (click)="addPassenger()" class="btn btn-ghost px-3 py-1.5 text-xs">
                  <ng-icon name="phosphorPlus" aria-hidden="true" />{{ 'vendor.desk.addPassenger' | t }}
                </button>
              </div>

              <button type="submit" [disabled]="submitting()" class="btn btn-primary w-full">
                <ng-icon name="phosphorTicket" aria-hidden="true" />
                {{ (submitting() ? 'vendor.desk.selling' : 'vendor.desk.sellTicket') | t }}
              </button>
            </form>
          </section>

          <section class="lg:col-span-2">
            <div class="mb-3 flex items-center justify-between gap-3">
              <h2 class="font-display font-semibold text-ink dark:text-white">{{ 'vendor.desk.recentBookings' | t }}</h2>
            </div>
            @if (loading()) {
              <p class="text-slate-500 dark:text-slate-400">{{ 'vendor.common.loading' | t }}</p>
            } @else if (bookings().length === 0) {
              <div class="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-slate-200 bg-slate-50/60 px-6 py-14 text-center dark:border-white/10 dark:bg-white/5">
                <span class="grid h-12 w-12 place-items-center rounded-full bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300">
                  <ng-icon name="phosphorTicket" class="text-2xl" aria-hidden="true" />
                </span>
                <p class="text-sm font-medium text-slate-600 dark:text-slate-300">{{ 'vendor.desk.empty' | t }}</p>
              </div>
            } @else {
              <div class="stagger-children space-y-2">
                @for (b of bookings(); track b.bookingId) {
                  <div class="card flex flex-wrap items-center justify-between gap-3 p-3.5">
                    <div class="min-w-0">
                      <p class="truncate font-medium text-slate-800 dark:text-slate-100">{{ b.origin }} → {{ b.destination }}</p>
                      <p class="text-xs tabular-nums text-slate-500 dark:text-slate-400">{{ b.customerEmail }} · {{ b.departureUtc | date: 'MMM d, HH:mm' }} · <span class="font-mono">{{ b.reference }}</span></p>
                    </div>
                    <div class="flex items-center gap-3">
                      <span class="text-sm font-bold tabular-nums text-slate-900 dark:text-white">{{ b.totalAmount | number: '1.0-0' }} {{ b.currency }}</span>
                      <span
                        class="badge"
                        [class]="
                          b.status === 'Confirmed' ? 'badge-brand' :
                          b.status === 'Cancelled' ? 'bg-rose-50 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300' :
                          'badge-accent'
                        "
                      >
                        {{ statusLabel(b.status) }}
                      </span>
                      @if (b.status !== 'Cancelled') {
                        @if (confirmingCancel() === b.bookingId) {
                          <button type="button" [disabled]="busy() === b.bookingId" (click)="cancel(b)" class="btn btn-danger px-2.5 py-1.5 text-xs">
                            <ng-icon name="phosphorCheck" aria-hidden="true" />{{ 'vendor.common.confirm' | t }}
                          </button>
                          <button type="button" (click)="confirmingCancel.set(null)" class="btn btn-ghost px-2.5 py-1.5 text-xs">{{ 'vendor.common.keep' | t }}</button>
                        } @else {
                          <button type="button" (click)="confirmingCancel.set(b.bookingId)" class="btn btn-ghost px-2.5 py-1.5 text-xs text-rose-600 hover:text-rose-700 dark:text-rose-400">
                            <ng-icon name="phosphorProhibit" aria-hidden="true" />{{ 'vendor.trips.cancel' | t }}
                          </button>
                        }
                      }
                    </div>
                  </div>
                }
              </div>
            }
          </section>
        </div>
      </div>
    </div>
  `,
})
export class VendorDeskComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(VendorApiService);
  private readonly toasts = inject(ToastService);
  private readonly i18n = inject(TranslationService);

  /** Translate the known booking statuses; unknown values fall back to the raw string. */
  protected statusLabel(status: string): string {
    if (status === 'Confirmed') return this.i18n.t('vendor.desk.status.confirmed');
    if (status === 'Cancelled') return this.i18n.t('vendor.desk.status.cancelled');
    return status;
  }

  protected readonly trips = signal<VendorTrip[]>([]);
  protected readonly bookings = signal<CompanyBooking[]>([]);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly confirmingCancel = signal<string | null>(null);
  protected readonly busy = signal<string | null>(null);

  protected readonly passengers = new FormArray<FormGroup>([this.newPassenger()]);
  protected readonly form = this.fb.group({
    tripId: this.fb.nonNullable.control('', [Validators.required]),
    customerEmail: this.fb.nonNullable.control('', [Validators.required, Validators.email]),
    passengers: this.passengers,
  });

  ngOnInit(): void {
    this.api.listTrips(1, 100).subscribe({
      next: (p) => this.trips.set(p.data.filter((t) => t.status === 'Scheduled')),
    });
    this.loadBookings();
  }

  protected addPassenger(): void {
    this.passengers.push(this.newPassenger());
  }

  protected removePassenger(index: number): void {
    this.passengers.removeAt(index);
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toasts.info(this.i18n.t('vendor.desk.toast.incomplete'));
      return;
    }
    const v = this.form.getRawValue();
    const rows = v.passengers as { firstName: string; lastName: string; seatNumber: number }[];
    const body: CounterBookingRequest = {
      tripId: v.tripId,
      customerEmail: v.customerEmail.trim().toLowerCase(),
      passengers: rows.map((p) => ({
        firstName: p.firstName.trim(),
        lastName: p.lastName.trim(),
        seatNumber: Number(p.seatNumber),
      })),
    };
    this.submitting.set(true);
    this.api.counterBooking(body).subscribe({
      next: (r) => {
        this.submitting.set(false);
        this.toasts.success(this.i18n.t('vendor.desk.toast.confirmed', { reference: r.reference, amount: r.totalAmount, currency: r.currency }));
        this.passengers.clear();
        this.passengers.push(this.newPassenger());
        this.form.patchValue({ tripId: '', customerEmail: '' });
        this.loadBookings();
      },
      error: () => this.submitting.set(false),
    });
  }

  protected cancel(b: CompanyBooking): void {
    this.busy.set(b.bookingId);
    this.api.cancelCompanyBooking(b.bookingId).subscribe({
      next: (r) => {
        this.busy.set(null);
        this.confirmingCancel.set(null);
        this.toasts.success(this.i18n.t(r.refundInitiated ? 'vendor.desk.toast.cancelledRefund' : 'vendor.desk.toast.cancelledCash'));
        this.loadBookings();
      },
      error: () => { this.busy.set(null); this.confirmingCancel.set(null); },
    });
  }

  private loadBookings(): void {
    this.loading.set(true);
    this.api.listCompanyBookings().subscribe({
      next: (p) => { this.bookings.set(p.data); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  private newPassenger(): FormGroup {
    return this.fb.nonNullable.group({
      firstName: ['', [Validators.required, Validators.maxLength(100)]],
      lastName: ['', [Validators.required, Validators.maxLength(100)]],
      seatNumber: [1, [Validators.required, Validators.min(1)]],
    });
  }
}
