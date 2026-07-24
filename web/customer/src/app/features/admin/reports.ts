import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  phosphorBuildings,
  phosphorBus,
  phosphorTicket,
  phosphorWallet,
} from '@ng-icons/phosphor-icons/regular';
import { AdminApiService } from '../../core/api/admin-api.service';
import { AdminCompanyReportRow, AdminSystemSummary, CurrencyAmount } from '../../core/models';
import { AdminNavComponent } from './admin-nav';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

const STATUS_KEY: Record<string, string> = {
  Pending: 'admin.status.pending',
  Active: 'admin.status.active',
  Suspended: 'admin.status.suspended',
};

@Component({
  selector: 'app-admin-reports',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, AdminNavComponent, NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorBuildings, phosphorBus, phosphorTicket, phosphorWallet })],
  template: `
    <div class="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <div class="lg:flex lg:items-start lg:gap-8">
        <app-admin-nav />

        <div class="min-w-0 flex-1">
          <h1 class="animate-in mb-6 text-2xl font-bold text-slate-900 dark:text-white">{{ 'admin.reports.title' | t }}</h1>

          @if (loading()) {
            <!-- Fixed-footprint skeleton: mirrors the real tile grid + table so nothing jumps on load. -->
            <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3" aria-hidden="true">
              @for (i of [0, 1, 2]; track i) {
                <div class="card p-6">
                  <div class="h-3 w-24 animate-pulse rounded-full bg-slate-200 dark:bg-white/10"></div>
                  <div class="mt-4 h-8 w-16 animate-pulse rounded-lg bg-slate-200 dark:bg-white/10"></div>
                  <div class="mt-3 h-3 w-28 animate-pulse rounded-full bg-slate-100 dark:bg-white/5"></div>
                </div>
              }
            </div>
            <div class="card mt-8 p-6" aria-hidden="true">
              <div class="h-3 w-32 animate-pulse rounded-full bg-slate-200 dark:bg-white/10"></div>
              <div class="mt-5 space-y-3">
                @for (i of [0, 1, 2, 3]; track i) {
                  <div class="h-10 w-full animate-pulse rounded-lg bg-slate-100 dark:bg-white/5"></div>
                }
              </div>
            </div>
          } @else if (summary(); as s) {
            <!-- Hero stat tiles -->
            <div class="stagger-children grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <div class="card p-5 sm:p-6">
                <div class="flex items-center justify-between">
                  <p class="text-xs font-semibold uppercase tracking-wide text-slate-400 dark:text-slate-500">{{ 'admin.reports.companies' | t }}</p>
                  <span class="grid h-9 w-9 place-items-center rounded-xl bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300">
                    <ng-icon name="phosphorBuildings" class="text-base" />
                  </span>
                </div>
                <p class="mt-3 font-display text-3xl font-bold tabular-nums text-slate-900 dark:text-white">{{ s.companies }}</p>
                <p class="mt-1 text-sm text-slate-500 dark:text-slate-400">{{ 'admin.reports.activeCount' | t: { count: s.activeCompanies } }}</p>
              </div>

              <div class="card p-5 sm:p-6">
                <div class="flex items-center justify-between">
                  <p class="text-xs font-semibold uppercase tracking-wide text-slate-400 dark:text-slate-500">{{ 'admin.reports.trips' | t }}</p>
                  <span class="grid h-9 w-9 place-items-center rounded-xl bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300">
                    <ng-icon name="phosphorBus" class="text-base" />
                  </span>
                </div>
                <p class="mt-3 font-display text-3xl font-bold tabular-nums text-slate-900 dark:text-white">{{ s.trips }}</p>
              </div>

              <div class="card p-5 sm:p-6">
                <div class="flex items-center justify-between">
                  <p class="text-xs font-semibold uppercase tracking-wide text-slate-400 dark:text-slate-500">{{ 'admin.reports.confirmedBookings' | t }}</p>
                  <span class="grid h-9 w-9 place-items-center rounded-xl bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300">
                    <ng-icon name="phosphorTicket" class="text-base" />
                  </span>
                </div>
                <p class="mt-3 font-display text-3xl font-bold tabular-nums text-slate-900 dark:text-white">{{ s.confirmedBookings }}</p>
              </div>

              <div class="card p-5 sm:col-span-2 sm:p-6 lg:col-span-3">
                <div class="flex items-center justify-between">
                  <p class="text-xs font-semibold uppercase tracking-wide text-slate-400 dark:text-slate-500">{{ 'admin.reports.revenue' | t }}</p>
                  <span class="grid h-9 w-9 place-items-center rounded-xl bg-accent-300/25 text-accent-600 dark:bg-accent-400/15 dark:text-accent-300">
                    <ng-icon name="phosphorWallet" class="text-base" />
                  </span>
                </div>
                @if (s.revenue.length === 0) {
                  <p class="mt-3 font-display text-2xl font-bold text-slate-400 dark:text-slate-600">—</p>
                } @else {
                  <div class="mt-3 flex flex-wrap gap-3">
                    @for (r of s.revenue; track r.currency) {
                      <div class="flex items-baseline gap-1.5 rounded-2xl bg-accent-300/15 px-4 py-2 dark:bg-accent-400/10">
                        <span class="font-display text-2xl font-bold tabular-nums text-ink dark:text-accent-300">{{ r.amount | number: '1.0-0' }}</span>
                        <span class="text-sm font-semibold text-accent-600 dark:text-accent-300">{{ r.currency }}</span>
                      </div>
                    }
                  </div>
                }
              </div>
            </div>

            <h2 class="mb-3 mt-8 text-lg font-bold text-slate-900 dark:text-white">{{ 'admin.reports.byCompany' | t }}</h2>
            @if (companies().length === 0) {
              <div class="card flex flex-col items-center gap-3 border-dashed px-6 py-14 text-center">
                <span class="grid h-12 w-12 place-items-center rounded-full bg-slate-100 text-slate-400 dark:bg-white/5 dark:text-slate-500">
                  <ng-icon name="phosphorBuildings" class="text-2xl" />
                </span>
                <p class="text-sm font-medium text-slate-500 dark:text-slate-400">{{ 'admin.reports.noCompanies' | t }}</p>
              </div>
            } @else {
              <div class="card overflow-x-auto">
                <table class="w-full text-start text-sm">
                  <thead class="border-b border-slate-100 text-start text-xs font-semibold uppercase tracking-wide text-slate-400 dark:border-white/10 dark:text-slate-500">
                    <tr>
                      <th class="px-4 py-3 text-start font-semibold sm:px-6">{{ 'admin.reports.colCompany' | t }}</th>
                      <th class="px-4 py-3 text-start font-semibold sm:px-6">{{ 'admin.reports.colStatus' | t }}</th>
                      <th class="px-4 py-3 text-end font-semibold sm:px-6">{{ 'admin.reports.colTrips' | t }}</th>
                      <th class="px-4 py-3 text-end font-semibold sm:px-6">{{ 'admin.reports.colConfirmed' | t }}</th>
                      <th class="px-4 py-3 text-end font-semibold sm:px-6">{{ 'admin.reports.colRevenue' | t }}</th>
                    </tr>
                  </thead>
                  <tbody class="stagger-children divide-y divide-slate-100 dark:divide-white/5">
                    @for (c of companies(); track c.companyId) {
                      <tr class="transition-colors hover:bg-slate-50 dark:hover:bg-white/3">
                        <td class="px-4 py-3 font-semibold text-slate-800 dark:text-slate-100 sm:px-6">{{ c.name }}</td>
                        <td class="px-4 py-3 sm:px-6">
                          <span [class]="'badge ' + statusBadgeClass(c.status)">{{ statusKey(c.status) | t }}</span>
                        </td>
                        <td class="px-4 py-3 text-end tabular-nums text-slate-600 dark:text-slate-300 sm:px-6">{{ c.trips }}</td>
                        <td class="px-4 py-3 text-end tabular-nums text-slate-600 dark:text-slate-300 sm:px-6">{{ c.confirmedBookings }}</td>
                        <td class="px-4 py-3 text-end font-semibold tabular-nums text-slate-800 dark:text-slate-100 sm:px-6">{{ revenueLabel(c.revenue) }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }
          }
        </div>
      </div>
    </div>
  `,
})
export class AdminReportsComponent implements OnInit {
  private readonly api = inject(AdminApiService);

  protected readonly summary = signal<AdminSystemSummary | null>(null);
  protected readonly companies = signal<AdminCompanyReportRow[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    this.api.systemSummary().subscribe({
      next: (s) => { this.summary.set(s); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
    this.api.companyReport().subscribe({ next: (c) => this.companies.set(c), error: () => undefined });
  }

  protected statusKey(status: string): string {
    return STATUS_KEY[status] ?? status;
  }

  /** Tailwind classes for the status badge — appended to the shared `.badge` base class. */
  protected statusBadgeClass(status: string): string {
    if (status === 'Active') return 'badge-brand';
    if (status === 'Pending') return 'badge-accent';
    return 'bg-rose-50 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300';
  }

  protected revenueLabel(revenue: CurrencyAmount[]): string {
    if (revenue.length === 0) return '—';
    return revenue.map((r) => `${Math.round(r.amount).toLocaleString()} ${r.currency}`).join(' · ');
  }
}
