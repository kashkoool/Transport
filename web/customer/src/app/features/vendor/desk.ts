import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { CounterBookingRequest, VendorApiService } from '../../core/api/vendor-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { CompanyBooking, VendorTrip } from '../../core/models';
import { VendorNavComponent } from './vendor-nav';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-vendor-desk',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, VendorNavComponent, TranslatePipe],
  template: `
    <app-vendor-nav />
    <h1 class="mb-6 text-2xl font-bold text-slate-900">{{ 'vendor.desk.title' | t }}</h1>

    <div class="grid gap-6 lg:grid-cols-3">
      <section class="rounded-xl border border-slate-200 bg-white p-4">
        <h2 class="mb-3 font-semibold text-slate-900">{{ 'vendor.desk.newBooking' | t }}</h2>
        <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3">
          <div>
            <label for="tripId" class="mb-1 block text-sm font-medium text-slate-700">{{ 'vendor.desk.trip' | t }}</label>
            <select id="tripId" formControlName="tripId" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500">
              <option value="">{{ 'vendor.common.select' | t }}</option>
              @for (t of trips(); track t.id) {
                <option [value]="t.id">{{ t.origin }} → {{ t.destination }} · {{ t.departureUtc | date: 'MMM d, HH:mm' }}</option>
              }
            </select>
          </div>
          <div>
            <label for="customerEmail" class="mb-1 block text-sm font-medium text-slate-700">{{ 'vendor.desk.customerEmail' | t }}</label>
            <input id="customerEmail" type="email" formControlName="customerEmail" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
            <p class="mt-1 text-xs text-slate-400">{{ 'vendor.desk.emailHint' | t }}</p>
          </div>

          <div formArrayName="passengers" class="space-y-2">
            <p class="text-sm font-medium text-slate-700">{{ 'vendor.desk.passengers' | t }}</p>
            @for (g of passengers.controls; track $index) {
              <div [formGroupName]="$index" class="flex items-center gap-2">
                <input type="text" formControlName="firstName" [placeholder]="'vendor.desk.firstName' | t" class="w-1/3 rounded-md border border-slate-300 px-2 py-1.5 text-sm focus:border-brand-500 focus:outline-none" />
                <input type="text" formControlName="lastName" [placeholder]="'vendor.desk.lastName' | t" class="w-1/3 rounded-md border border-slate-300 px-2 py-1.5 text-sm focus:border-brand-500 focus:outline-none" />
                <input type="number" min="1" formControlName="seatNumber" [placeholder]="'vendor.desk.seat' | t" class="w-1/4 rounded-md border border-slate-300 px-2 py-1.5 text-sm focus:border-brand-500 focus:outline-none" />
                @if (passengers.length > 1) {
                  <button type="button" (click)="removePassenger($index)" class="text-rose-500 hover:text-rose-700">✕</button>
                }
              </div>
            }
            <button type="button" (click)="addPassenger()" class="text-sm font-medium text-brand-600 hover:text-brand-700">{{ 'vendor.desk.addPassenger' | t }}</button>
          </div>

          <button type="submit" [disabled]="submitting()" class="w-full rounded-md bg-emerald-600 px-4 py-2 font-medium text-white hover:bg-emerald-700 disabled:opacity-50">
            {{ (submitting() ? 'vendor.desk.selling' : 'vendor.desk.sellTicket') | t }}
          </button>
        </form>
      </section>

      <section class="lg:col-span-2">
        <h2 class="mb-2 font-semibold text-slate-900">{{ 'vendor.desk.recentBookings' | t }}</h2>
        @if (loading()) {
          <p class="text-slate-500">{{ 'vendor.common.loading' | t }}</p>
        } @else if (bookings().length === 0) {
          <p class="rounded-lg bg-slate-100 p-4 text-slate-600">{{ 'vendor.desk.empty' | t }}</p>
        } @else {
          <div class="space-y-2">
            @for (b of bookings(); track b.bookingId) {
              <div class="flex items-center justify-between rounded-xl border border-slate-200 bg-white p-3">
                <div>
                  <p class="font-medium text-slate-800">{{ b.origin }} → {{ b.destination }}</p>
                  <p class="text-xs text-slate-500">{{ b.customerEmail }} · {{ b.departureUtc | date: 'MMM d, HH:mm' }} · <span class="font-mono">{{ b.reference }}</span></p>
                </div>
                <div class="flex items-center gap-3">
                  <span class="text-sm font-bold text-slate-900">{{ b.totalAmount | number: '1.0-0' }} {{ b.currency }}</span>
                  <span class="rounded-full px-2 py-0.5 text-xs font-semibold"
                    [class.bg-emerald-100]="b.status === 'Confirmed'" [class.text-emerald-700]="b.status === 'Confirmed'"
                    [class.bg-rose-100]="b.status === 'Cancelled'" [class.text-rose-700]="b.status === 'Cancelled'"
                    [class.bg-amber-100]="b.status !== 'Confirmed' && b.status !== 'Cancelled'" [class.text-amber-700]="b.status !== 'Confirmed' && b.status !== 'Cancelled'">
                    {{ statusLabel(b.status) }}
                  </span>
                  @if (b.status !== 'Cancelled') {
                    @if (confirmingCancel() === b.bookingId) {
                      <button type="button" [disabled]="busy() === b.bookingId" (click)="cancel(b)" class="text-sm font-medium text-rose-700">{{ 'vendor.common.confirm' | t }}</button>
                      <button type="button" (click)="confirmingCancel.set(null)" class="text-sm text-slate-500">{{ 'vendor.common.keep' | t }}</button>
                    } @else {
                      <button type="button" (click)="confirmingCancel.set(b.bookingId)" class="text-sm font-medium text-rose-600 hover:text-rose-700">{{ 'vendor.trips.cancel' | t }}</button>
                    }
                  }
                </div>
              </div>
            }
          </div>
        }
      </section>
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
