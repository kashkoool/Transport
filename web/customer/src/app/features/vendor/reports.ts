import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  phosphorCurrencyCircleDollar,
  phosphorCheckCircle,
  phosphorUsers,
  phosphorGauge,
  phosphorFileCsv,
  phosphorFileXls,
  phosphorFilePdf,
  phosphorTrendUp,
  phosphorFolderOpen,
} from '@ng-icons/phosphor-icons/regular';
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
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, VendorNavComponent, NgIcon, TranslatePipe],
  providers: [
    provideIcons({
      phosphorCurrencyCircleDollar,
      phosphorCheckCircle,
      phosphorUsers,
      phosphorGauge,
      phosphorFileCsv,
      phosphorFileXls,
      phosphorFilePdf,
      phosphorTrendUp,
      phosphorFolderOpen,
    }),
  ],
  template: `
    <div class="lg:grid lg:grid-cols-[15rem_1fr] lg:items-start lg:gap-8">
      <app-vendor-nav />

      <div class="min-w-0">
        <h1 class="animate-in mb-6 font-display text-2xl font-bold text-ink dark:text-white">{{ 'vendor.reports.title' | t }}</h1>

        <form [formGroup]="rangeForm" (ngSubmit)="load()" class="card mb-6 flex flex-wrap items-end gap-3 p-4">
          <div>
            <label for="from" class="label">{{ 'vendor.reports.from' | t }}</label>
            <input id="from" type="date" formControlName="from" class="input px-3 py-2" />
          </div>
          <div>
            <label for="to" class="label">{{ 'vendor.reports.to' | t }}</label>
            <input id="to" type="date" formControlName="to" class="input px-3 py-2" />
          </div>
          <button type="submit" class="btn btn-primary px-4 py-2 text-sm">{{ 'vendor.reports.apply' | t }}</button>
          <span class="flex-1"></span>
          @if (tab() !== 'employees') {
            <div class="flex gap-2">
              <button type="button" (click)="download('csv')" class="btn btn-ghost px-3 py-2 text-sm">
                <ng-icon name="phosphorFileCsv" aria-hidden="true" />CSV
              </button>
              <button type="button" (click)="download('xlsx')" class="btn btn-ghost px-3 py-2 text-sm">
                <ng-icon name="phosphorFileXls" aria-hidden="true" />XLSX
              </button>
              <button type="button" (click)="download('pdf')" class="btn btn-ghost px-3 py-2 text-sm">
                <ng-icon name="phosphorFilePdf" aria-hidden="true" />PDF
              </button>
            </div>
          }
        </form>

        @if (summary(); as s) {
          <div class="mb-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <div class="card p-4">
              <div class="mb-2 flex items-center gap-2 text-slate-400 dark:text-slate-500">
                <ng-icon name="phosphorCurrencyCircleDollar" class="text-lg" aria-hidden="true" />
                <p class="text-xs font-semibold tracking-wide uppercase">{{ 'vendor.reports.kpi.revenue' | t }}</p>
              </div>
              <p class="font-display text-2xl font-bold tabular-nums text-ink dark:text-white">{{ s.revenue | number: '1.0-0' }} <span class="text-sm font-medium text-slate-400">{{ s.currency }}</span></p>
            </div>
            <div class="card p-4">
              <div class="mb-2 flex items-center gap-2 text-slate-400 dark:text-slate-500">
                <ng-icon name="phosphorCheckCircle" class="text-lg" aria-hidden="true" />
                <p class="text-xs font-semibold tracking-wide uppercase">{{ 'vendor.reports.kpi.confirmedBookings' | t }}</p>
              </div>
              <p class="font-display text-2xl font-bold tabular-nums text-ink dark:text-white">{{ s.confirmedBookings }}</p>
            </div>
            <div class="card p-4">
              <div class="mb-2 flex items-center gap-2 text-slate-400 dark:text-slate-500">
                <ng-icon name="phosphorUsers" class="text-lg" aria-hidden="true" />
                <p class="text-xs font-semibold tracking-wide uppercase">{{ 'vendor.reports.kpi.seatsSold' | t }}</p>
              </div>
              <p class="font-display text-2xl font-bold tabular-nums text-ink dark:text-white">{{ s.seatsSold }} <span class="text-sm font-medium text-slate-400">/ {{ s.seatsOffered }}</span></p>
            </div>
            <div class="card p-4">
              <div class="mb-2 flex items-center gap-2 text-slate-400 dark:text-slate-500">
                <ng-icon name="phosphorGauge" class="text-lg" aria-hidden="true" />
                <p class="text-xs font-semibold tracking-wide uppercase">{{ 'vendor.reports.kpi.occupancy' | t }}</p>
              </div>
              <p class="font-display text-2xl font-bold tabular-nums text-ink dark:text-white">{{ s.occupancyPct }}%</p>
            </div>
          </div>
        }

        <div class="grid gap-6 lg:grid-cols-3">
          <section class="lg:col-span-2">
            <nav class="mb-3 flex gap-1 border-b border-slate-200 dark:border-white/10">
              @for (t of tabs; track t.key) {
                <button type="button" (click)="setTab(t.key)"
                  class="-mb-px border-b-2 px-4 py-2 text-sm font-medium transition-colors"
                  [class]="tab() === t.key ? 'border-brand-600 text-brand-700 dark:text-brand-300' : 'border-transparent text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-slate-200'">
                  {{ t.label | t }}
                </button>
              }
            </nav>

            @if (loading()) {
              <p class="text-slate-500 dark:text-slate-400">{{ 'vendor.common.loading' | t }}</p>
            } @else if (tab() === 'trips') {
              @if (trips().length === 0) {
                <div class="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-slate-200 bg-slate-50/60 px-6 py-14 text-center dark:border-white/10 dark:bg-white/5">
                  <span class="grid h-12 w-12 place-items-center rounded-full bg-slate-100 text-slate-400 dark:bg-white/10 dark:text-slate-400">
                    <ng-icon name="phosphorFolderOpen" class="text-2xl" aria-hidden="true" />
                  </span>
                  <p class="text-sm font-medium text-slate-600 dark:text-slate-300">{{ 'vendor.reports.emptyTrips' | t }}</p>
                </div>
              } @else {
                <div class="card overflow-hidden">
                  <div class="overflow-x-auto">
                    <table class="w-full text-sm">
                      <thead class="bg-slate-50 text-xs font-semibold tracking-wide text-slate-500 uppercase dark:bg-white/5 dark:text-slate-400">
                        <tr>
                          <th class="px-3 py-3 text-start font-semibold">{{ 'vendor.reports.th.route' | t }}</th>
                          <th class="px-3 py-3 text-start font-semibold">{{ 'vendor.reports.th.departs' | t }}</th>
                          <th class="px-3 py-3 text-end font-semibold">{{ 'vendor.reports.th.sold' | t }}</th>
                          <th class="px-3 py-3 text-end font-semibold">{{ 'vendor.reports.th.revenue' | t }}</th>
                          <th class="px-3 py-3 text-start font-semibold">{{ 'vendor.reports.th.status' | t }}</th>
                        </tr>
                      </thead>
                      <tbody class="stagger-children divide-y divide-slate-100 dark:divide-white/10">
                        @for (r of trips(); track r.tripId) {
                          <tr class="transition-colors hover:bg-slate-50 dark:hover:bg-white/3">
                            <td class="px-3 py-3 font-medium text-slate-800 dark:text-slate-100">{{ r.origin }} → {{ r.destination }}</td>
                            <td class="px-3 py-3 tabular-nums text-slate-500 dark:text-slate-400">{{ r.departureUtc | date: 'MMM d, HH:mm' }}</td>
                            <td class="px-3 py-3 text-end tabular-nums text-slate-700 dark:text-slate-200">{{ r.seatsSold }} / {{ r.seatCount }}</td>
                            <td class="px-3 py-3 text-end tabular-nums font-medium text-slate-800 dark:text-slate-100">{{ r.revenue | number: '1.0-0' }} {{ r.currency }}</td>
                            <td class="px-3 py-3 text-slate-500 dark:text-slate-400">{{ r.status }}</td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                </div>
              }
            } @else if (tab() === 'bookings') {
              @if (bookings().length === 0) {
                <div class="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-slate-200 bg-slate-50/60 px-6 py-14 text-center dark:border-white/10 dark:bg-white/5">
                  <span class="grid h-12 w-12 place-items-center rounded-full bg-slate-100 text-slate-400 dark:bg-white/10 dark:text-slate-400">
                    <ng-icon name="phosphorFolderOpen" class="text-2xl" aria-hidden="true" />
                  </span>
                  <p class="text-sm font-medium text-slate-600 dark:text-slate-300">{{ 'vendor.reports.emptyBookings' | t }}</p>
                </div>
              } @else {
                <div class="card overflow-hidden">
                  <div class="overflow-x-auto">
                    <table class="w-full text-sm">
                      <thead class="bg-slate-50 text-xs font-semibold tracking-wide text-slate-500 uppercase dark:bg-white/5 dark:text-slate-400">
                        <tr>
                          <th class="px-3 py-3 text-start font-semibold">{{ 'vendor.reports.th.reference' | t }}</th>
                          <th class="px-3 py-3 text-start font-semibold">{{ 'vendor.reports.th.customer' | t }}</th>
                          <th class="px-3 py-3 text-start font-semibold">{{ 'vendor.reports.th.status' | t }}</th>
                          <th class="px-3 py-3 text-end font-semibold">{{ 'vendor.reports.th.total' | t }}</th>
                          <th class="px-3 py-3 text-start font-semibold">{{ 'vendor.reports.th.gateway' | t }}</th>
                        </tr>
                      </thead>
                      <tbody class="stagger-children divide-y divide-slate-100 dark:divide-white/10">
                        @for (b of bookings(); track b.bookingId) {
                          <tr class="transition-colors hover:bg-slate-50 dark:hover:bg-white/3">
                            <td class="px-3 py-3 font-mono text-slate-800 dark:text-slate-100">{{ b.reference }}</td>
                            <td class="px-3 py-3 text-slate-500 dark:text-slate-400">{{ b.customerEmail }}</td>
                            <td class="px-3 py-3 text-slate-500 dark:text-slate-400">{{ b.status }}</td>
                            <td class="px-3 py-3 text-end tabular-nums font-medium text-slate-800 dark:text-slate-100">{{ b.totalAmount | number: '1.0-0' }} {{ b.currency }}</td>
                            <td class="px-3 py-3 text-slate-500 dark:text-slate-400">{{ b.gateway }}</td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                </div>
              }
            } @else {
              @if (employees().length === 0) {
                <div class="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-slate-200 bg-slate-50/60 px-6 py-14 text-center dark:border-white/10 dark:bg-white/5">
                  <span class="grid h-12 w-12 place-items-center rounded-full bg-slate-100 text-slate-400 dark:bg-white/10 dark:text-slate-400">
                    <ng-icon name="phosphorFolderOpen" class="text-2xl" aria-hidden="true" />
                  </span>
                  <p class="text-sm font-medium text-slate-600 dark:text-slate-300">{{ 'vendor.reports.emptyEmployees' | t }}</p>
                </div>
              } @else {
                <div class="card overflow-hidden">
                  <div class="overflow-x-auto">
                    <table class="w-full text-sm">
                      <thead class="bg-slate-50 text-xs font-semibold tracking-wide text-slate-500 uppercase dark:bg-white/5 dark:text-slate-400">
                        <tr>
                          <th class="px-3 py-3 text-start font-semibold">{{ 'vendor.reports.th.employee' | t }}</th>
                          <th class="px-3 py-3 text-start font-semibold">{{ 'vendor.reports.th.email' | t }}</th>
                          <th class="px-3 py-3 text-end font-semibold">{{ 'vendor.reports.th.bookings' | t }}</th>
                          <th class="px-3 py-3 text-end font-semibold">{{ 'vendor.reports.th.revenue' | t }}</th>
                        </tr>
                      </thead>
                      <tbody class="stagger-children divide-y divide-slate-100 dark:divide-white/10">
                        @for (e of employees(); track e.staffId) {
                          <tr class="transition-colors hover:bg-slate-50 dark:hover:bg-white/3">
                            <td class="px-3 py-3 font-medium text-slate-800 dark:text-slate-100">{{ e.fullName }}</td>
                            <td class="px-3 py-3 text-slate-500 dark:text-slate-400">{{ e.email }}</td>
                            <td class="px-3 py-3 text-end tabular-nums text-slate-700 dark:text-slate-200">{{ e.bookings }}</td>
                            <td class="px-3 py-3 text-end tabular-nums font-medium text-slate-800 dark:text-slate-100">{{ e.revenue | number: '1.0-0' }} {{ e.currency }}</td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                </div>
              }
            }
          </section>

          <section class="card p-5">
            <h2 class="mb-3 flex items-center gap-2 font-display font-semibold text-ink dark:text-white">
              <ng-icon name="phosphorTrendUp" class="text-brand-600 dark:text-brand-400" aria-hidden="true" />
              {{ 'vendor.reports.demandForecast' | t }}
            </h2>
            <form [formGroup]="demandForm" (ngSubmit)="predict()" class="space-y-3">
              <div class="grid grid-cols-2 gap-2.5">
                <input type="text" formControlName="origin" [placeholder]="'common.from' | t" class="input px-3 py-2" />
                <input type="text" formControlName="destination" [placeholder]="'common.to' | t" class="input px-3 py-2" />
              </div>
              <input type="date" formControlName="date" class="input px-3 py-2" />
              <button type="submit" [disabled]="predicting()" class="btn btn-dark w-full">
                {{ (predicting() ? 'vendor.reports.forecasting' : 'vendor.reports.forecast') | t }}
              </button>
            </form>
            @if (demand(); as d) {
              <div class="mt-3 rounded-xl bg-slate-50 p-3.5 text-sm dark:bg-white/5">
                <p class="font-display text-2xl font-bold tabular-nums text-ink dark:text-white">~{{ d.predictedBookings }} <span class="text-sm font-normal text-slate-500 dark:text-slate-400">{{ 'vendor.reports.bookingsLabel' | t }}</span></p>
                <p class="mt-1 text-slate-600 dark:text-slate-300">{{ 'vendor.reports.confidence' | t }} <span class="font-medium capitalize">{{ d.confidence }}</span> {{ 'vendor.reports.pastBookings' | t: { n: d.sampleSize } }}</p>
              </div>
            }
          </section>
        </div>
      </div>
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
