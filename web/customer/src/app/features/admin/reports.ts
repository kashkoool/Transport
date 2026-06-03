import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { AdminApiService } from '../../core/api/admin-api.service';
import { AdminSystemSummary } from '../../core/models';
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
      <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
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
          <p class="text-xs uppercase text-slate-400">Total revenue (completed payments)</p>
          <p class="text-3xl font-bold text-slate-900">{{ s.revenue | number: '1.0-0' }}</p>
        </div>
      </div>
    }
  `,
})
export class AdminReportsComponent implements OnInit {
  private readonly api = inject(AdminApiService);

  protected readonly summary = signal<AdminSystemSummary | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    this.api.systemSummary().subscribe({
      next: (s) => { this.summary.set(s); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
