import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { BookingFlow } from '../../core/booking-flow';
import { TripRealtimeService } from '../../core/notifications/trip-realtime.service';
import { ToastService } from '../../core/toast/toast.service';
import { HoldResult, PromoPreview, ReviewSummary, SeatMap, TripStop } from '../../core/models';

const MAX_SEATS = 10;
const DOCUMENT_TYPES = ['National ID', 'Passport', 'Driver License'] as const;

@Component({
  selector: 'app-booking',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DecimalPipe, DatePipe],
  template: `
    @if (trip(); as t) {
      <button type="button" class="text-sm text-brand-600 hover:text-brand-700" (click)="back()">
        ← Back to search
      </button>
      <h1 class="mt-2 text-2xl font-bold text-slate-900">{{ t.origin }} → {{ t.destination }}</h1>
      <p class="mb-6 text-slate-500">{{ t.price | number: '1.0-0' }} {{ t.currency }} per seat</p>

      @if (stops().length > 0) {
        <section class="mb-6 rounded-xl border border-slate-200 bg-white p-4">
          <h2 class="mb-2 font-semibold text-slate-900">Route</h2>
          <ol class="space-y-1 text-sm text-slate-600">
            <li class="flex justify-between"><span class="font-medium text-slate-900">{{ t.origin }}</span><span>{{ t.departureUtc | date: 'HH:mm' }}</span></li>
            @for (s of stops(); track s.sequence) {
              <li class="flex justify-between pl-3">
                <span>↳ {{ s.name }}</span>
                <span>{{ s.arrivalUtc ? (s.arrivalUtc | date: 'HH:mm') : '—' }}</span>
              </li>
            }
            <li class="flex justify-between"><span class="font-medium text-slate-900">{{ t.destination }}</span><span>{{ t.arrivalUtc | date: 'HH:mm' }}</span></li>
          </ol>
        </section>
      }

      @if (reviews(); as r) {
        @if (r.count > 0) {
          <section class="mb-6 rounded-xl border border-slate-200 bg-white p-4">
            <h2 class="mb-2 font-semibold text-slate-900">
              Reviews
              <span class="ml-1 text-sm font-normal text-amber-500">★ {{ r.averageRating | number: '1.1-1' }}</span>
              <span class="text-sm font-normal text-slate-400">({{ r.count }})</span>
            </h2>
            <ul class="space-y-2">
              @for (rev of r.reviews; track rev.id) {
                <li class="text-sm">
                  <span class="text-amber-400">{{ stars(rev.rating) }}</span>
                  <span class="font-medium text-slate-700">{{ rev.displayName }}</span>
                  @if (rev.comment) { <span class="italic text-slate-500">“{{ rev.comment }}”</span> }
                </li>
              }
            </ul>
          </section>
        }
      }

      @if (!held()) {
        <section class="rounded-xl border border-slate-200 bg-white p-4">
          <h2 class="mb-3 font-semibold text-slate-900">Choose your seats</h2>
          <p class="mb-3 text-sm text-slate-500">Pick up to {{ maxSeats }} seats. Greyed seats are taken.</p>
          <div class="space-y-2">
            @for (row of rows(); track $index) {
              <div class="flex justify-center gap-2">
                @for (seat of row; track seat) {
                  <button
                    type="button"
                    [disabled]="isTaken(seat)"
                    (click)="toggleSeat(seat)"
                    class="h-10 w-10 rounded-md border text-sm font-medium"
                    [class.bg-brand-600]="isSelected(seat)"
                    [class.text-white]="isSelected(seat)"
                    [class.border-brand-600]="isSelected(seat)"
                    [class.bg-slate-200]="isTaken(seat)"
                    [class.text-slate-400]="isTaken(seat)"
                    [class.cursor-not-allowed]="isTaken(seat)"
                    [class.border-slate-300]="!isSelected(seat) && !isTaken(seat)"
                    [class.text-slate-700]="!isSelected(seat) && !isTaken(seat)"
                  >
                    {{ seat }}
                  </button>
                }
              </div>
            }
          </div>
          <button
            type="button"
            [disabled]="selectedSeats().length === 0 || holding()"
            (click)="hold()"
            class="mt-4 rounded-md bg-brand-600 px-4 py-2 font-medium text-white hover:bg-brand-700 disabled:opacity-50"
          >
            {{ holding() ? 'Holding…' : 'Hold ' + selectedSeats().length + ' seat(s)' }}
          </button>
        </section>
      } @else {
        <section class="rounded-xl border border-slate-200 bg-white p-4">
          <div class="mb-4 flex items-center justify-between">
            <h2 class="font-semibold text-slate-900">Passenger details</h2>
            <span
              class="rounded-full px-3 py-1 text-sm font-medium"
              [class.bg-amber-100]="remaining() > 0"
              [class.text-amber-700]="remaining() > 0"
              [class.bg-rose-100]="remaining() === 0"
              [class.text-rose-700]="remaining() === 0"
            >
              {{ remaining() > 0 ? 'Held · ' + countdown() + ' left' : 'Hold expired' }}
            </span>
          </div>

          <form [formGroup]="passengerForm" (ngSubmit)="confirm()" class="space-y-4">
            <div formArrayName="passengers" class="space-y-4">
              @for (group of passengers.controls; track $index) {
                <div [formGroupName]="$index" class="rounded-lg border border-slate-100 p-3">
                  <p class="mb-2 text-sm font-semibold text-slate-700">Seat {{ group.value.seatNumber }}</p>
                  <div class="grid gap-3 sm:grid-cols-2">
                    <input
                      type="text"
                      formControlName="firstName"
                      placeholder="First name"
                      class="rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
                    />
                    <input
                      type="text"
                      formControlName="lastName"
                      placeholder="Last name"
                      class="rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
                    />
                    <select
                      formControlName="documentType"
                      class="rounded-md border border-slate-300 px-3 py-2 text-slate-700 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
                    >
                      <option value="">ID document (optional)</option>
                      @for (dt of documentTypes; track dt) {
                        <option [value]="dt">{{ dt }}</option>
                      }
                    </select>
                    <input
                      type="text"
                      formControlName="documentNumber"
                      placeholder="Document number (optional)"
                      class="rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
                    />
                  </div>
                </div>
              }
            </div>

            <div class="rounded-lg border border-slate-100 p-3">
              <label for="promo-code" class="mb-1 block text-sm font-medium text-slate-700">Promo code</label>
              <div class="flex gap-2">
                <input
                  id="promo-code"
                  type="text"
                  [value]="promoCode()"
                  (input)="onPromoInput($event)"
                  placeholder="e.g. SAVE20"
                  class="flex-1 rounded-md border border-slate-300 px-3 py-2 uppercase focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
                />
                <button
                  type="button"
                  [disabled]="!promoCode() || promoChecking()"
                  (click)="applyPromo()"
                  class="rounded-md bg-slate-800 px-4 py-2 text-sm font-medium text-white hover:bg-slate-900 disabled:opacity-50"
                >
                  {{ promoChecking() ? 'Checking…' : 'Apply' }}
                </button>
              </div>
              @if (promo(); as p) {
                <p class="mt-2 text-sm text-emerald-700">
                  −{{ p.discount | number: '1.0-0' }} {{ p.currency }} applied ({{ p.code }}).
                </p>
              }
            </div>

            <div class="flex items-center justify-between border-t border-slate-100 pt-4">
              <span class="text-lg font-bold text-slate-900">
                Total: {{ total() | number: '1.0-0' }} {{ t.currency }}
              </span>
              <button
                type="submit"
                [disabled]="submitting() || remaining() === 0"
                class="rounded-md bg-emerald-600 px-5 py-2 font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
              >
                {{ submitting() ? 'Creating…' : 'Confirm & continue to payment' }}
              </button>
            </div>
          </form>
        </section>
      }
    }
  `,
})
export class BookingComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly toasts = inject(ToastService);
  private readonly router = inject(Router);
  private readonly realtime = inject(TripRealtimeService);
  protected readonly flow = inject(BookingFlow);

  protected readonly maxSeats = MAX_SEATS;
  protected readonly documentTypes = DOCUMENT_TYPES;
  protected readonly trip = this.flow.trip;
  protected readonly seatMap = signal<SeatMap | null>(null);
  protected readonly stops = signal<TripStop[]>([]);
  protected readonly reviews = signal<ReviewSummary | null>(null);
  protected readonly selectedSeats = signal<number[]>([]);
  protected readonly held = signal(false);
  protected readonly holding = signal(false);
  protected readonly submitting = signal(false);
  protected readonly remaining = signal(0); // seconds until the hold expires
  protected readonly promoCode = signal('');
  protected readonly promo = signal<PromoPreview | null>(null);
  protected readonly promoChecking = signal(false);

  private readonly takenSet = computed(() => new Set(this.seatMap()?.takenSeats ?? []));
  protected readonly total = computed(() => {
    const gross = (this.trip()?.price ?? 0) * this.selectedSeats().length;
    const p = this.promo();
    return p ? Math.max(0, gross - p.discount) : gross;
  });

  protected readonly passengers = new FormArray<FormGroup>([]);
  protected readonly passengerForm = this.fb.group({ passengers: this.passengers });

  private timer?: ReturnType<typeof setInterval>;
  private stopWatching?: () => void;

  constructor() {
    if (!this.trip()) {
      this.toasts.info('Please pick a trip first.');
      this.router.navigate(['/search']);
    }
  }

  ngOnInit(): void {
    const trip = this.trip();
    if (!trip) return;
    this.loadSeatMap();
    this.api.tripStops(trip.id).subscribe({ next: (s) => this.stops.set(s), error: () => undefined });
    this.api.tripReviews(trip.id).subscribe({ next: (r) => this.reviews.set(r), error: () => undefined });
    // Live updates: refresh the map (and drop a now-taken selection) when others book/cancel.
    this.stopWatching = this.realtime.watchTrip(trip.id, () => this.onLiveSeatUpdate());
  }

  /** Seats grouped into rows of the bus's seats-per-row, for the seat-map grid. */
  protected rows(): number[][] {
    const map = this.seatMap();
    const count = map?.seatCount ?? this.trip()?.seatCount ?? 0;
    const perRow = Math.max(1, map?.seatsPerRow ?? 4);
    const all = Array.from({ length: count }, (_, i) => i + 1);
    const rows: number[][] = [];
    for (let i = 0; i < all.length; i += perRow) rows.push(all.slice(i, i + perRow));
    return rows;
  }

  /** A 1–5 rating as filled/empty star glyphs for compact display. */
  protected stars(rating: number): string {
    const n = Math.max(0, Math.min(5, Math.round(rating)));
    return '★'.repeat(n) + '☆'.repeat(5 - n);
  }

  protected isTaken(seat: number): boolean {
    return this.takenSet().has(seat);
  }

  protected isSelected(seat: number): boolean {
    return this.selectedSeats().includes(seat);
  }

  protected toggleSeat(seat: number): void {
    if (this.isTaken(seat)) return;
    this.selectedSeats.update((seats) => {
      if (seats.includes(seat)) return seats.filter((s) => s !== seat);
      if (seats.length >= MAX_SEATS) {
        this.toasts.info(`You can hold at most ${MAX_SEATS} seats.`);
        return seats;
      }
      return [...seats, seat].sort((a, b) => a - b);
    });
  }

  protected hold(): void {
    const trip = this.trip();
    if (!trip || this.selectedSeats().length === 0) return;
    this.holding.set(true);
    this.api.holdSeats(trip.id, this.selectedSeats()).subscribe({
      next: (res) => {
        this.holding.set(false);
        this.held.set(true);
        this.buildPassengerForms();
        this.startCountdown(res);
      },
      error: () => {
        this.holding.set(false); // 409 etc. surfaces as a toast; refresh the map so taken seats show
        this.loadSeatMap();
      },
    });
  }

  protected onPromoInput(event: Event): void {
    this.promoCode.set((event.target as HTMLInputElement).value.toUpperCase());
    this.promo.set(null); // a changed code invalidates a prior preview
  }

  protected applyPromo(): void {
    const trip = this.trip();
    const code = this.promoCode().trim();
    if (!trip || !code) return;
    this.promoChecking.set(true);
    this.api.previewPromo(trip.id, code, this.selectedSeats().length).subscribe({
      next: (preview) => {
        this.promoChecking.set(false);
        this.promo.set(preview);
        this.toasts.success('Promo code applied.');
      },
      error: () => {
        this.promoChecking.set(false);
        this.promo.set(null); // invalid/expired surfaces as a toast from the interceptor
      },
    });
  }

  protected confirm(): void {
    const trip = this.trip();
    if (!trip) return;
    if (this.passengerForm.invalid) {
      this.passengerForm.markAllAsTouched();
      this.toasts.info('Please enter every passenger’s name.');
      return;
    }
    this.submitting.set(true);
    const passengers = this.passengers.getRawValue() as {
      firstName: string;
      lastName: string;
      seatNumber: number;
      documentType: string;
      documentNumber: string;
    }[];
    this.api.createBooking(trip.id, passengers, this.promo()?.code ?? null).subscribe({
      next: (booking) => this.router.navigate(['/pay', booking.bookingId]),
      error: () => this.submitting.set(false),
    });
  }

  protected back(): void {
    this.router.navigate(['/search']);
  }

  protected countdown(): string {
    const s = this.remaining();
    const m = Math.floor(s / 60);
    const sec = s % 60;
    return `${m}:${sec.toString().padStart(2, '0')}`;
  }

  private loadSeatMap(): void {
    const trip = this.trip();
    if (!trip) return;
    this.api.seatMap(trip.id).subscribe({ next: (m) => this.seatMap.set(m), error: () => undefined });
  }

  private onLiveSeatUpdate(): void {
    if (this.held()) return; // once held, our seats are committed — the picker is gone
    const trip = this.trip();
    if (!trip) return;
    this.api.seatMap(trip.id).subscribe({
      next: (m) => {
        this.seatMap.set(m);
        const taken = new Set(m.takenSeats);
        const clashes = this.selectedSeats().filter((s) => taken.has(s));
        if (clashes.length > 0) {
          this.selectedSeats.update((seats) => seats.filter((s) => !taken.has(s)));
          this.toasts.info('Some seats were just taken. Please reselect.');
        }
      },
      error: () => undefined,
    });
  }

  private buildPassengerForms(): void {
    this.passengers.clear();
    for (const seat of this.selectedSeats()) {
      this.passengers.push(
        this.fb.nonNullable.group({
          firstName: ['', [Validators.required, Validators.maxLength(100)]],
          lastName: ['', [Validators.required, Validators.maxLength(100)]],
          seatNumber: [seat],
          documentType: ['', [Validators.maxLength(60)]],
          documentNumber: ['', [Validators.maxLength(60)]],
        }),
      );
    }
  }

  private startCountdown(hold: HoldResult): void {
    const expiry = new Date(hold.expiresAtUtc).getTime();
    const tick = () => {
      const secs = Math.max(0, Math.round((expiry - Date.now()) / 1000));
      this.remaining.set(secs);
      if (secs === 0 && this.timer) {
        clearInterval(this.timer);
        this.timer = undefined;
      }
    };
    tick();
    this.timer = setInterval(tick, 1000);
  }

  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
    this.stopWatching?.();
  }
}
