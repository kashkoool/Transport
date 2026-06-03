import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { AdminApiService } from '../../core/api/admin-api.service';
import { AdminCompanyReportRow, AdminSystemSummary, CurrencyAmount } from '../../core/models';
import { AdminNavComponent } from './admin-nav';

@Component({
  selector: 'app-admin-reports',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, AdminNavComponent],
  template: `
    <app-admin-nav />
    <h1 class="mb-6 text-2xl font-bold text-slate-900">Platform overview</h1>

    @if (loading()) {
      <p class="text-slate-500">Loading…</p>
    } @else if (summary(); as s) {
      <div class="mb-8 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <div class="rounded-xl border border-slate-200 bg-white p-5">
          <p class="text-xs uppercase text-slate-400">Companies</p>
          <p class="text-2xl font-bold text-slate-900">{{ s.companies }}</p>
          <p class="text-sm text-slate-500">{{ s.activeCompanies }} active</p>
        </div>
        <div class="rounded-xl border border-slate-200 bg-white p-5">
          <p class="text-xs uppercase text-slate-400">Trips</p>
          <p class="text-2xl font-bold text-slate-900">{{ s.trips }}</p>
        </div>
        <div class="rounded-xl border border-slate-200 bg-white p-5">
          <p class="text-xs uppercase text-slate-400">Confirmed bookings</p>
          <p class="text-2xl font-bold text-slate-900">{{ s.confirmedBookings }}</p>
        </div>
        <div class="rounded-xl border border-slate-200 bg-white p-5 sm:col-span-2 lg:col-span-3">
          <p class="text-xs uppercase text-slate-400">Revenue (completed payments, by currency)</p>
          @if (s.revenue.length === 0) {
            <p class="text-xl font-bold text-slate-900">—</p>
          } @else {
            <div class="flex flex-wrap gap-6">
              @for (r of s.revenue; track r.currency) {
                <p class="text-2xl font-bold text-slate-900">{{ r.amount | number: '1.0-0' }} <span class="text-sm font-normal text-slate-500">{{ r.currency }}</span></p>
              }
            </div>
          }
        </div>
      </div>

      <h2 class="mb-2 font-semibold text-slate-900">By company</h2>
      @if (companies().length === 0) {
        <p class="rounded-lg bg-slate-100 p-4 text-slate-600">No companies yet.</p>
      } @else {
        <div class="overflow-x-auto rounded-xl border border-slate-200 bg-white">
          <table class="w-full text-sm">
            <thead class="bg-slate-50 text-left text-slate-500">
              <tr>
                <th class="px-4 py-2 font-medium">Company</th>
                <th class="px-4 py-2 font-medium">Status</th>
                <th class="px-4 py-2 font-medium">Trips</th>
                <th class="px-4 py-2 font-medium">Confirmed</th>
                <th class="px-4 py-2 font-medium">Revenue</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100">
              @for (c of companies(); track c.companyId) {
                <tr>
                  <td class="px-4 py-2 font-medium text-slate-800">{{ c.name }}</td>
                  <td class="px-4 py-2 text-slate-500">{{ c.status }}</td>
                  <td class="px-4 py-2">{{ c.trips }}</td>
                  <td class="px-4 py-2">{{ c.confirmedBookings }}</td>
                  <td class="px-4 py-2">{{ revenueLabel(c.revenue) }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    }
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

  protected revenueLabel(revenue: CurrencyAmount[]): string {
    if (revenue.length === 0) return '—';
    return revenue.map((r) => `${Math.round(r.amount).toLocaleString()} ${r.currency}`).join(' · ');
  }
}
