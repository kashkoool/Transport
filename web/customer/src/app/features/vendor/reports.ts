import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
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

type Tab = 'trips' | 'bookings' | 'employees';

@Component({
  selector: 'app-vendor-reports',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, VendorNavComponent],
  template: `
    <app-vendor-nav />
    <h1 class="mb-6 text-2xl font-bold text-slate-900">Reports</h1>

    <form [formGroup]="rangeForm" (ngSubmit)="load()" class="mb-6 flex flex-wrap items-end gap-3">
      <div>
        <label for="from" class="mb-1 block text-sm font-medium text-slate-700">From</label>
        <input id="from" type="date" formControlName="from" class="rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none" />
      </div>
      <div>
        <label for="to" class="mb-1 block text-sm font-medium text-slate-700">To</label>
        <input id="to" type="date" formControlName="to" class="rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none" />
      </div>
      <button type="submit" class="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700">Apply</button>
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
          <p class="text-xs uppercase text-slate-400">Revenue</p>
          <p class="text-xl font-bold text-slate-900">{{ s.revenue | number: '1.0-0' }} {{ s.currency }}</p>
        </div>
        <div class="rounded-xl border border-slate-200 bg-white p-4">
          <p class="text-xs uppercase text-slate-400">Confirmed bookings</p>
          <p class="text-xl font-bold text-slate-900">{{ s.confirmedBookings }}</p>
        </div>
        <div class="rounded-xl border border-slate-200 bg-white p-4">
          <p class="text-xs uppercase text-slate-400">Seats sold</p>
          <p class="text-xl font-bold text-slate-900">{{ s.seatsSold }} / {{ s.seatsOffered }}</p>
        </div>
        <div class="rounded-xl border border-slate-200 bg-white p-4">
          <p class="text-xs uppercase text-slate-400">Occupancy</p>
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
              [class.border-indigo-600]="tab() === t.key" [class.text-indigo-700]="tab() === t.key"
              [class.border-transparent]="tab() !== t.key" [class.text-slate-500]="tab() !== t.key">
              {{ t.label }}
            </button>
          }
        </nav>

        @if (loading()) {
          <p class="text-slate-500">Loading…</p>
        } @else if (tab() === 'trips') {
          @if (trips().length === 0) { <p class="rounded-lg bg-slate-100 p-4 text-slate-600">No trips in this range.</p> }
          @else {
            <div class="overflow-x-auto rounded-xl border border-slate-200 bg-white">
              <table class="w-full text-sm">
                <thead class="bg-slate-50 text-left text-slate-500"><tr>
                  <th class="px-3 py-2 font-medium">Route</th><th class="px-3 py-2 font-medium">Departs</th>
                  <th class="px-3 py-2 font-medium">Sold</th><th class="px-3 py-2 font-medium">Revenue</th><th class="px-3 py-2 font-medium">Status</th>
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
          @if (bookings().length === 0) { <p class="rounded-lg bg-slate-100 p-4 text-slate-600">No bookings in this range.</p> }
          @else {
            <div class="overflow-x-auto rounded-xl border border-slate-200 bg-white">
              <table class="w-full text-sm">
                <thead class="bg-slate-50 text-left text-slate-500"><tr>
                  <th class="px-3 py-2 font-medium">Reference</th><th class="px-3 py-2 font-medium">Customer</th>
                  <th class="px-3 py-2 font-medium">Status</th><th class="px-3 py-2 font-medium">Total</th><th class="px-3 py-2 font-medium">Gateway</th>
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
          @if (employees().length === 0) { <p class="rounded-lg bg-slate-100 p-4 text-slate-600">No desk-booking activity in this range.</p> }
          @else {
            <div class="overflow-x-auto rounded-xl border border-slate-200 bg-white">
              <table class="w-full text-sm">
                <thead class="bg-slate-50 text-left text-slate-500"><tr>
                  <th class="px-3 py-2 font-medium">Employee</th><th class="px-3 py-2 font-medium">Email</th>
                  <th class="px-3 py-2 font-medium">Bookings</th><th class="px-3 py-2 font-medium">Revenue</th>
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
        <h2 class="mb-3 font-semibold text-slate-900">Demand forecast</h2>
        <form [formGroup]="demandForm" (ngSubmit)="predict()" class="space-y-3">
          <div class="grid grid-cols-2 gap-2">
            <input type="text" formControlName="origin" placeholder="From" class="rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none" />
            <input type="text" formControlName="destination" placeholder="To" class="rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none" />
          </div>
          <input type="date" formControlName="date" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none" />
          <button type="submit" [disabled]="predicting()" class="w-full rounded-md bg-slate-800 px-4 py-2 text-sm font-medium text-white hover:bg-slate-900 disabled:opacity-50">
            {{ predicting() ? 'Forecasting…' : 'Forecast' }}
          </button>
        </form>
        @if (demand(); as d) {
          <div class="mt-3 rounded-lg bg-slate-50 p-3 text-sm">
            <p class="text-2xl font-bold text-slate-900">~{{ d.predictedBookings }} <span class="text-sm font-normal text-slate-500">bookings</span></p>
            <p class="text-slate-600">Confidence: <span class="font-medium capitalize">{{ d.confidence }}</span> ({{ d.sampleSize }} past bookings)</p>
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

  protected readonly tabs: { key: Tab; label: string }[] = [
    { key: 'trips', label: 'Trips' },
    { key: 'bookings', label: 'Bookings' },
    { key: 'employees', label: 'Employees' },
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
    this.api.reportSummary(from, to).subscribe({ next: (s) => this.summary.set(s) });
    this.api.tripReport(from, to).subscribe({ next: (r) => this.trips.set(r) });
    this.api.bookingReport(from, to).subscribe({ next: (r) => this.bookings.set(r) });
    this.api.employeeReport(from, to).subscribe({
      next: (r) => { this.employees.set(r); this.loading.set(false); },
      error: () => this.loading.set(false),
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
      error: () => this.toasts.error('Could not download the report.'),
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
