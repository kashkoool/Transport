import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { ToastService } from '../../core/toast/toast.service';
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
        <a routerLink="/search" class="mt-2 inline-block font-medium text-brand-600 hover:text-brand-700">
          Find a trip →
        </a>
      </div>
    } @else {
      <div class="space-y-3">
        @for (b of bookings(); track b.bookingId) {
          <div class="rounded-xl border border-slate-200 bg-white p-4">
            <div class="flex items-center justify-between">
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
                  [class.bg-rose-100]="b.status === 'Cancelled'"
                  [class.text-rose-700]="b.status === 'Cancelled'"
                  [class.bg-amber-100]="b.status !== 'Confirmed' && b.status !== 'Cancelled'"
                  [class.text-amber-700]="b.status !== 'Confirmed' && b.status !== 'Cancelled'"
                >
                  {{ b.status }}
                </span>
                <p class="mt-1 text-sm font-bold text-slate-900">
                  {{ b.totalAmount | number: '1.0-0' }} {{ b.currency }}
                </p>
              </div>
            </div>

            <div class="mt-3 flex flex-wrap items-center gap-2 border-t border-slate-100 pt-3">
              <a [routerLink]="['/ticket', b.bookingId]" class="text-sm font-medium text-brand-600 hover:text-brand-700">
                View ticket →
              </a>

              @if (b.status !== 'Cancelled') {
                @if (confirmingCancel() === b.bookingId) {
                  <span class="text-sm text-slate-600">Cancel this booking?</span>
                  <button type="button" [disabled]="busy() === b.bookingId" (click)="cancel(b.bookingId)"
                    class="rounded-md bg-rose-600 px-3 py-1 text-sm font-medium text-white hover:bg-rose-700 disabled:opacity-50">
                    {{ busy() === b.bookingId ? 'Cancelling…' : 'Yes, cancel' }}
                  </button>
                  <button type="button" (click)="confirmingCancel.set(null)" class="text-sm text-slate-500 hover:text-slate-700">Keep</button>
                } @else {
                  <button type="button" (click)="confirmingCancel.set(b.bookingId)" class="text-sm font-medium text-rose-600 hover:text-rose-700">
                    Cancel
                  </button>
                }
                <button type="button" (click)="toggleReview(b.bookingId)" class="text-sm font-medium text-slate-600 hover:text-slate-800">
                  Leave a review
                </button>
              }
            </div>

            @if (reviewing() === b.bookingId) {
              <div class="mt-3 rounded-lg border border-slate-100 bg-slate-50 p-3">
                <div class="mb-2 flex items-center gap-1">
                  @for (star of stars; track star) {
                    <button type="button" (click)="reviewRating.set(star)"
                      class="text-2xl leading-none"
                      [class.text-amber-400]="star <= reviewRating()"
                      [class.text-slate-300]="star > reviewRating()">★</button>
                  }
                </div>
                <textarea
                  [value]="reviewComment()"
                  (input)="reviewComment.set($any($event.target).value)"
                  rows="2"
                  maxlength="1000"
                  placeholder="How was your trip? (optional)"
                  class="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
                ></textarea>
                <div class="mt-2 flex gap-2">
                  <button type="button" [disabled]="busy() === b.bookingId" (click)="submitReview(b.bookingId)"
                    class="rounded-md bg-brand-600 px-3 py-1 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50">
                    {{ busy() === b.bookingId ? 'Submitting…' : 'Submit review' }}
                  </button>
                  <button type="button" (click)="reviewing.set(null)" class="text-sm text-slate-500 hover:text-slate-700">Cancel</button>
                </div>
              </div>
            }
          </div>
        }
      </div>
    }
  `,
})
export class MyBookingsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toasts = inject(ToastService);

  protected readonly stars = [1, 2, 3, 4, 5];
  protected readonly bookings = signal<BookingSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly confirmingCancel = signal<string | null>(null);
  protected readonly reviewing = signal<string | null>(null);
  protected readonly reviewRating = signal(5);
  protected readonly reviewComment = signal('');
  protected readonly busy = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  protected cancel(bookingId: string): void {
    this.busy.set(bookingId);
    this.api.cancelBooking(bookingId).subscribe({
      next: (res) => {
        this.busy.set(null);
        this.confirmingCancel.set(null);
        this.toasts.success(res.refundInitiated ? 'Booking cancelled. A refund is on its way.' : 'Booking cancelled.');
        this.load();
      },
      error: () => {
        this.busy.set(null);
        this.confirmingCancel.set(null);
      },
    });
  }

  protected toggleReview(bookingId: string): void {
    this.reviewing.set(this.reviewing() === bookingId ? null : bookingId);
    this.reviewRating.set(5);
    this.reviewComment.set('');
  }

  protected submitReview(bookingId: string): void {
    this.busy.set(bookingId);
    this.api.createReview(bookingId, this.reviewRating(), this.reviewComment().trim() || null).subscribe({
      next: () => {
        this.busy.set(null);
        this.reviewing.set(null);
        this.toasts.success('Thanks for your review!');
      },
      error: () => this.busy.set(null),
    });
  }

  private load(): void {
    this.loading.set(true);
    this.api.myBookings().subscribe({
      next: (list) => {
        this.bookings.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
