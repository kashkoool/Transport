import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorTicket } from '@ng-icons/phosphor-icons/regular';
import { ApiService } from '../../core/api/api.service';
import { ToastService } from '../../core/toast/toast.service';
import { BookingSummary } from '../../core/models';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-my-bookings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DecimalPipe, RouterLink, NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorTicket })],
  template: `
    <h1 class="animate-in mb-6 text-2xl font-bold text-slate-900 dark:text-slate-100">{{ 'myBookings.title' | t }}</h1>

    @if (loading()) {
      <div class="stagger-children space-y-3">
        @for (s of [1, 2, 3]; track s) {
          <div class="card flex animate-pulse items-center justify-between gap-4 p-5">
            <div class="space-y-2">
              <div class="h-4 w-40 rounded bg-slate-100 dark:bg-white/10"></div>
              <div class="h-3 w-56 rounded bg-slate-100 dark:bg-white/10"></div>
            </div>
            <div class="h-8 w-20 rounded-full bg-slate-100 dark:bg-white/10"></div>
          </div>
        }
      </div>
    } @else if (bookings().length === 0) {
      <div class="card flex flex-col items-center gap-2 p-12 text-center">
        <ng-icon name="phosphorTicket" class="text-4xl text-slate-300 dark:text-slate-600" />
        <p class="mt-2 text-slate-600 dark:text-slate-300">{{ 'myBookings.empty' | t }}</p>
        <a routerLink="/search" class="btn btn-primary mt-4">
          {{ 'myBookings.findTrip' | t }}
        </a>
      </div>
    } @else {
      <div class="stagger-children space-y-3">
        @for (b of bookings(); track b.bookingId) {
          <div class="card card-interactive p-5">
            <div class="flex items-center justify-between gap-3">
              <div>
                <p class="font-semibold text-slate-900 dark:text-slate-100">{{ b.origin }} → {{ b.destination }}</p>
                <p class="text-sm text-slate-500 dark:text-slate-400">{{ b.departureUtc | date: 'EEE, MMM d • HH:mm' }}</p>
                <p class="font-mono text-xs text-slate-400 dark:text-slate-500">{{ b.reference }}</p>
              </div>
              <div class="text-end">
                <span
                  class="badge"
                  [class]="
                    b.status === 'Confirmed'
                      ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-400/15 dark:text-emerald-300'
                      : b.status === 'Cancelled'
                        ? 'bg-rose-100 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300'
                        : 'bg-amber-100 text-amber-700 dark:bg-amber-400/15 dark:text-amber-300'
                  "
                >
                  {{ b.status }}
                </span>
                <p class="mt-1 text-sm font-bold text-slate-900 dark:text-slate-100">
                  {{ b.totalAmount | number: '1.0-0' }} {{ b.currency }}
                </p>
              </div>
            </div>

            <div class="mt-3 flex flex-wrap items-center gap-2 border-t border-slate-100 pt-3 dark:border-white/10">
              <a [routerLink]="['/ticket', b.bookingId]" class="btn btn-soft px-3 py-1.5 text-sm">
                <ng-icon name="phosphorTicket" /> {{ 'myBookings.viewTicket' | t }}
              </a>

              @if (b.status !== 'Cancelled') {
                @if (confirmingCancel() === b.bookingId) {
                  <span class="text-sm text-slate-600 dark:text-slate-400">{{ 'myBookings.cancelConfirm' | t }}</span>
                  <button type="button" [disabled]="busy() === b.bookingId" (click)="cancel(b.bookingId)"
                    class="btn btn-danger px-3 py-1.5 text-sm">
                    {{ (busy() === b.bookingId ? 'myBookings.cancelling' : 'myBookings.yesCancel') | t }}
                  </button>
                  <button type="button" (click)="confirmingCancel.set(null)" class="btn btn-ghost px-3 py-1.5 text-sm">{{ 'myBookings.keep' | t }}</button>
                } @else {
                  <button type="button" (click)="confirmingCancel.set(b.bookingId)"
                    class="cursor-pointer text-sm font-semibold text-rose-600 transition hover:text-rose-700 dark:text-rose-400 dark:hover:text-rose-300">
                    {{ 'myBookings.cancel' | t }}
                  </button>
                }
                <button type="button" (click)="toggleReview(b.bookingId)"
                  class="cursor-pointer text-sm font-semibold text-slate-600 transition hover:text-slate-800 dark:text-slate-400 dark:hover:text-slate-200">
                  {{ 'myBookings.leaveReview' | t }}
                </button>
              }
            </div>

            @if (reviewing() === b.bookingId) {
              <div class="mt-3 rounded-2xl ring-1 ring-slate-200 dark:ring-white/10 bg-slate-50 p-4 dark:bg-white/5">
                <div class="mb-2 flex items-center gap-1">
                  @for (star of stars; track star) {
                    <button type="button" (click)="reviewRating.set(star)"
                      class="cursor-pointer text-2xl leading-none transition hover:scale-110"
                      [class]="star <= reviewRating() ? 'text-amber-400' : 'text-slate-300 dark:text-slate-600'">★</button>
                  }
                </div>
                <textarea
                  [value]="reviewComment()"
                  (input)="reviewComment.set($any($event.target).value)"
                  rows="2"
                  maxlength="1000"
                  [placeholder]="'myBookings.reviewPlaceholder' | t"
                  class="input text-sm"
                ></textarea>
                <div class="mt-2 flex gap-2">
                  <button type="button" [disabled]="busy() === b.bookingId" (click)="submitReview(b.bookingId)"
                    class="btn btn-primary px-3 py-1.5 text-sm">
                    {{ (busy() === b.bookingId ? 'myBookings.submitting' : 'myBookings.submitReview') | t }}
                  </button>
                  <button type="button" (click)="reviewing.set(null)" class="btn btn-ghost px-3 py-1.5 text-sm">{{ 'myBookings.cancel' | t }}</button>
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
  private readonly i18n = inject(TranslationService);

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
        this.toasts.success(this.i18n.t(res.refundInitiated ? 'myBookings.cancelledRefund' : 'myBookings.cancelled'));
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
        this.toasts.success(this.i18n.t('myBookings.reviewThanks'));
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
