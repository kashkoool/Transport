import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { BookingSummary } from '../../core/models';

@Component({
  selector: 'app-my-bookings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DecimalPipe, RouterLink],
  template: `
    <h1 class="mb-6 text-2xl font-bold text-slate-900">My bookings</h1>

    @if (loading()) {
      <p class="text-slate-500">Loading…</p>
    } @else if (bookings().length === 0) {
      <div class="rounded-xl bg-slate-100 p-6 text-center">
        <p class="text-slate-600">You have no bookings yet.</p>
        <a routerLink="/search" class="mt-2 inline-block font-medium text-indigo-600 hover:text-indigo-700">
          Find a trip →
        </a>
      </div>
    } @else {
      <div class="space-y-3">
        @for (b of bookings(); track b.bookingId) {
          <a
            [routerLink]="['/ticket', b.bookingId]"
            class="flex items-center justify-between rounded-xl border border-slate-200 bg-white p-4 hover:border-indigo-300 hover:shadow-sm"
          >
            <div>
              <p class="font-semibold text-slate-900">{{ b.origin }} → {{ b.destination }}</p>
              <p class="text-sm text-slate-500">{{ b.departureUtc | date: 'EEE, MMM d • HH:mm' }}</p>
              <p class="font-mono text-xs text-slate-400">{{ b.reference }}</p>
            </div>
            <div class="text-right">
              <span
                class="rounded-full px-3 py-1 text-xs font-semibold"
                [class.bg-emerald-100]="b.status === 'Confirmed'"
                [class.text-emerald-700]="b.status === 'Confirmed'"
                [class.bg-amber-100]="b.status !== 'Confirmed'"
                [class.text-amber-700]="b.status !== 'Confirmed'"
              >
                {{ b.status }}
              </span>
              <p class="mt-1 text-sm font-bold text-slate-900">
                {{ b.totalAmount | number: '1.0-0' }} {{ b.currency }}
              </p>
            </div>
          </a>
        }
      </div>
    }
  `,
})
export class MyBookingsComponent implements OnInit {
  private readonly api = inject(ApiService);

  protected readonly bookings = signal<BookingSummary[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    this.api.myBookings().subscribe({
      next: (list) => {
        this.bookings.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
