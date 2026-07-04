import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ReportFormat, VendorApiService } from '../../core/api/vendor-api.service';
import { ToastService } from '../../core/toast/toast.service';
import {
  BookingReportRow,
  DemandPrediction,
  EmployeeReportRow,
  TripReportRow,
  VendorReportSummary,
} from '../../core/models';
import { VendorNavComponent } from './vendor-nav';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

type Tab = 'trips' | 'bookings' | 'employees';

@Component({
  selector: 'app-vendor-reports',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, VendorNavComponent, TranslatePipe],
  template: `
    <app-vendor-nav />
    <h1 class="mb-6 text-2xl font-bold text-slate-900">{{ 'vendor.reports.title' | t }}</h1>

    <form [formGroup]="rangeForm" (ngSubmit)="load()" class="mb-6 flex flex-wrap items-end gap-3">
      <div>
        <label for="from" class="mb-1 block text-sm font-medium text-slate-700">{{ 'vendor.reports.from' | t }}</label>
        <input id="from" type="date" formControlName="from" class="rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none" />
      </div>
      <div>
        <label for="to" class="mb-1 block text-sm font-medium text-slate-700">{{ 'vendor.reports.to' | t }}</label>
        <input id="to" type="date" formControlName="to" class="rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none" />
      </div>
      <button type="submit" class="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700">{{ 'vendor.reports.apply' | t }}</button>
      <span class="flex-1"></span>
      @if (tab() !== 'employees') {
        <div class="flex gap-2">
          <button type="button" (click)="download('csv')" class="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">CSV</button>
          <button type="button" (click)="download('xlsx')" class="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">XLSX</button>
          <button type="button" (click)="download('pdf')" class="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">PDF</button>
        </div>
      }
    </form>

    @if (summary(); as s) {
      <div class="mb-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <div class="rounded-xl border border-slate-200 bg-white p-4">
          <p class="text-xs uppercase text-slate-400">{{ 'vendor.reports.kpi.revenue' | t }}</p>
          <p class="text-xl font-bold text-slate-900">{{ s.revenue | number: '1.0-0' }} {{ s.currency }}</p>
        </div>
        <div class="rounded-xl border border-slate-200 bg-white p-4">
          <p class="text-xs uppercase text-slate-400">{{ 'vendor.reports.kpi.confirmedBookings' | t }}</p>
          <p class="text-xl font-bold text-slate-900">{{ s.confirmedBookings }}</p>
        </div>
        <div class="rounded-xl border border-slate-200 bg-white p-4">
          <p class="text-xs uppercase text-slate-400">{{ 'vendor.reports.kpi.seatsSold' | t }}</p>
          <p class="text-xl font-bold text-slate-900">{{ s.seatsSold }} / {{ s.seatsOffered }}</p>
        </div>
        <div class="rounded-xl border border-slate-200 bg-white p-4">
          <p class="text-xs uppercase text-slate-400">{{ 'vendor.reports.kpi.occupancy' | t }}</p>
          <p class="text-xl font-bold text-slate-900">{{ s.occupancyPct }}%</p>
        </div>
      </div>
    }

    <div class="grid gap-6 lg:grid-cols-3">
      <section class="lg:col-span-2">
        <nav class="mb-3 flex gap-1 border-b border-slate-200">
          @for (t of tabs; track t.key) {
            <button type="button" (click)="setTab(t.key)"
              class="-mb-px border-b-2 px-4 py-2 text-sm font-medium"
              [class.border-brand-600]="tab() === t.key" [class.text-brand-700]="tab() === t.key"
              [class.border-transparent]="tab() !== t.key" [class.text-slate-500]="tab() !== t.key">
              {{ t.label | t }}
            </button>
          }
        </nav>

        @if (loading()) {
          <p class="text-slate-500">{{ 'vendor.common.loading' | t }}</p>
        } @else if (tab() === 'trips') {
          @if (trips().length === 0) { <p class="rounded-lg bg-slate-100 p-4 text-slate-600">{{ 'vendor.reports.emptyTrips' | t }}</p> }
          @else {
            <div class="overflow-x-auto rounded-xl border border-slate-200 bg-white">
              <table class="w-full text-sm">
                <thead class="bg-slate-50 text-left text-slate-500"><tr>
                  <th class="px-3 py-2 font-medium">{{ 'vendor.reports.th.route' | t }}</th><th class="px-3 py-2 font-medium">{{ 'vendor.reports.th.departs' | t }}</th>
                  <th class="px-3 py-2 font-medium">{{ 'vendor.reports.th.sold' | t }}</th><th class="px-3 py-2 font-medium">{{ 'vendor.reports.th.revenue' | t }}</th><th class="px-3 py-2 font-medium">{{ 'vendor.reports.th.status' | t }}</th>
                </tr></thead>
                <tbody class="divide-y divide-slate-100">
                  @for (r of trips(); track r.tripId) {
                    <tr>
                      <td class="px-3 py-2 font-medium text-slate-800">{{ r.origin }} → {{ r.destination }}</td>
                      <td class="px-3 py-2 text-slate-500">{{ r.departureUtc | date: 'MMM d, HH:mm' }}</td>
                      <td class="px-3 py-2">{{ r.seatsSold }} / {{ r.seatCount }}</td>
                      <td class="px-3 py-2">{{ r.revenue | number: '1.0-0' }} {{ r.currency }}</td>
                      <td class="px-3 py-2 text-slate-500">{{ r.status }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        } @else if (tab() === 'bookings') {
          @if (bookings().length === 0) { <p class="rounded-lg bg-slate-100 p-4 text-slate-600">{{ 'vendor.reports.emptyBookings' | t }}</p> }
          @else {
            <div class="overflow-x-auto rounded-xl border border-slate-200 bg-white">
              <table class="w-full text-sm">
                <thead class="bg-slate-50 text-left text-slate-500"><tr>
                  <th class="px-3 py-2 font-medium">{{ 'vendor.reports.th.reference' | t }}</th><th class="px-3 py-2 font-medium">{{ 'vendor.reports.th.customer' | t }}</th>
                  <th class="px-3 py-2 font-medium">{{ 'vendor.reports.th.status' | t }}</th><th class="px-3 py-2 font-medium">{{ 'vendor.reports.th.total' | t }}</th><th class="px-3 py-2 font-medium">{{ 'vendor.reports.th.gateway' | t }}</th>
                </tr></thead>
                <tbody class="divide-y divide-slate-100">
                  @for (b of bookings(); track b.bookingId) {
                    <tr>
                      <td class="px-3 py-2 font-mono text-slate-800">{{ b.reference }}</td>
                      <td class="px-3 py-2 text-slate-500">{{ b.customerEmail }}</td>
                      <td class="px-3 py-2 text-slate-500">{{ b.status }}</td>
                      <td class="px-3 py-2">{{ b.totalAmount | number: '1.0-0' }} {{ b.currency }}</td>
                      <td class="px-3 py-2 text-slate-500">{{ b.gateway }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        } @else {
          @if (employees().length === 0) { <p class="rounded-lg bg-slate-100 p-4 text-slate-600">{{ 'vendor.reports.emptyEmployees' | t }}</p> }
          @else {
            <div class="overflow-x-auto rounded-xl border border-slate-200 bg-white">
              <table class="w-full text-sm">
                <thead class="bg-slate-50 text-left text-slate-500"><tr>
                  <th class="px-3 py-2 font-medium">{{ 'vendor.reports.th.employee' | t }}</th><th class="px-3 py-2 font-medium">{{ 'vendor.reports.th.email' | t }}</th>
                  <th class="px-3 py-2 font-medium">{{ 'vendor.reports.th.bookings' | t }}</th><th class="px-3 py-2 font-medium">{{ 'vendor.reports.th.revenue' | t }}</th>
                </tr></thead>
                <tbody class="divide-y divide-slate-100">
                  @for (e of employees(); track e.staffId) {
                    <tr>
                      <td class="px-3 py-2 font-medium text-slate-800">{{ e.fullName }}</td>
                      <td class="px-3 py-2 text-slate-500">{{ e.email }}</td>
                      <td class="px-3 py-2">{{ e.bookings }}</td>
                      <td class="px-3 py-2">{{ e.revenue | number: '1.0-0' }} {{ e.currency }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        }
      </section>

      <section class="rounded-xl border border-slate-200 bg-white p-4">
        <h2 class="mb-3 font-semibold text-slate-900">{{ 'vendor.reports.demandForecast' | t }}</h2>
        <form [formGroup]="demandForm" (ngSubmit)="predict()" class="space-y-3">
          <div class="grid grid-cols-2 gap-2">
            <input type="text" formControlName="origin" [placeholder]="'common.from' | t" class="rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none" />
            <input type="text" formControlName="destination" [placeholder]="'common.to' | t" class="rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none" />
          </div>
          <input type="date" formControlName="date" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none" />
          <button type="submit" [disabled]="predicting()" class="w-full rounded-md bg-slate-800 px-4 py-2 text-sm font-medium text-white hover:bg-slate-900 disabled:opacity-50">
            {{ (predicting() ? 'vendor.reports.forecasting' : 'vendor.reports.forecast') | t }}
          </button>
        </form>
        @if (demand(); as d) {
          <div class="mt-3 rounded-lg bg-slate-50 p-3 text-sm">
            <p class="text-2xl font-bold text-slate-900">~{{ d.predictedBookings }} <span class="text-sm font-normal text-slate-500">{{ 'vendor.reports.bookingsLabel' | t }}</span></p>
            <p class="text-slate-600">{{ 'vendor.reports.confidence' | t }} <span class="font-medium capitalize">{{ d.confidence }}</span> {{ 'vendor.reports.pastBookings' | t: { n: d.sampleSize } }}</p>
          </div>
        }
      </section>
    </div>
  `,
})
export class VendorReportsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(VendorApiService);
  private readonly toasts = inject(ToastService);
  private readonly i18n = inject(TranslationService);

  protected readonly tabs: { key: Tab; label: string }[] = [
    { key: 'trips', label: 'vendor.reports.tab.trips' },
    { key: 'bookings', label: 'vendor.reports.tab.bookings' },
    { key: 'employees', label: 'vendor.reports.tab.employees' },
  ];
  protected readonly tab = signal<Tab>('trips');
  protected readonly summary = signal<VendorReportSummary | null>(null);
  protected readonly trips = signal<TripReportRow[]>([]);
  protected readonly bookings = signal<BookingReportRow[]>([]);
  protected readonly employees = signal<EmployeeReportRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly demand = signal<DemandPrediction | null>(null);
  protected readonly predicting = signal(false);

  protected readonly rangeForm = this.fb.nonNullable.group({ from: [''], to: [''] });
  protected readonly demandForm = this.fb.nonNullable.group({
    origin: ['', [Validators.required]],
    destination: ['', [Validators.required]],
    date: ['', [Validators.required]],
  });

  ngOnInit(): void {
    this.load();
  }

  protected setTab(tab: Tab): void {
    this.tab.set(tab);
  }

  protected load(): void {
    this.loading.set(true);
    const { from, to } = this.range();
    // Load all sections together so no tab waits on another's request; a failed section degrades
    // to empty (the error interceptor already surfaces the failure) rather than blocking the rest.
    forkJoin({
      summary: this.api.reportSummary(from, to).pipe(catchError(() => of(null))),
      trips: this.api.tripReport(from, to).pipe(catchError(() => of([] as TripReportRow[]))),
      bookings: this.api.bookingReport(from, to).pipe(catchError(() => of([] as BookingReportRow[]))),
      employees: this.api.employeeReport(from, to).pipe(catchError(() => of([] as EmployeeReportRow[]))),
    }).subscribe((r) => {
      if (r.summary) this.summary.set(r.summary);
      this.trips.set(r.trips);
      this.bookings.set(r.bookings);
      this.employees.set(r.employees);
      this.loading.set(false);
    });
  }

  protected download(format: ReportFormat): void {
    const { from, to } = this.range();
    const which = this.tab();
    const stream$ = which === 'bookings'
      ? this.api.exportBookingReport(format, from, to)
      : this.api.exportTripReport(format, from, to);
    const name = which === 'bookings' ? 'bookings-report' : 'trips-report';
    stream$.subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${name}.${format}`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toasts.error(this.i18n.t('vendor.reports.toast.downloadFailed')),
    });
  }

  protected predict(): void {
    if (this.demandForm.invalid) {
      this.demandForm.markAllAsTouched();
      return;
    }
    const v = this.demandForm.getRawValue();
    this.predicting.set(true);
    this.api.predictDemand(v.origin.trim(), v.destination.trim(), v.date).subscribe({
      next: (d) => { this.predicting.set(false); this.demand.set(d); },
      error: () => this.predicting.set(false),
    });
  }

  /** Convert the date inputs to ISO instants (start-of-day) the API expects, or undefined. */
  private range(): { from?: string; to?: string } {
    const v = this.rangeForm.getRawValue();
    return {
      from: v.from ? new Date(v.from).toISOString() : undefined,
      to: v.to ? new Date(v.to).toISOString() : undefined,
    };
  }
}
