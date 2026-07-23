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
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  phosphorArrowLeft,
  phosphorBus,
  phosphorClockCountdown,
  phosphorMapPinSimple,
  phosphorPhone,
  phosphorTag,
} from '@ng-icons/phosphor-icons/regular';
import { ApiService } from '../../core/api/api.service';
import { BookingFlow } from '../../core/booking-flow';
import { TripRealtimeService } from '../../core/notifications/trip-realtime.service';
import { ToastService } from '../../core/toast/toast.service';
import { HoldResult, PromoPreview, ReviewSummary, SeatMap, TripStop } from '../../core/models';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

const MAX_SEATS = 10;
const DOCUMENT_TYPES = ['National ID', 'Passport', 'Driver License'] as const;

@Component({
  selector: 'app-booking',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DecimalPipe, DatePipe, NgIcon, TranslatePipe],
  providers: [
    provideIcons({ phosphorArrowLeft, phosphorBus, phosphorClockCountdown, phosphorMapPinSimple, phosphorPhone, phosphorTag }),
  ],
  template: `
    @if (trip(); as t) {
      <button
        type="button"
        class="inline-flex cursor-pointer items-center gap-1.5 text-sm font-semibold text-brand-600 transition hover:text-brand-700 dark:text-brand-400 dark:hover:text-brand-300"
        (click)="back()"
      >
        <ng-icon name="phosphorArrowLeft" class="rtl:rotate-180" /> {{ 'booking.backToSearch' | t }}
      </button>
      <h1 class="animate-in mt-2 flex items-center gap-2 text-2xl font-bold text-slate-900 dark:text-slate-100">
        <span class="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-linear-to-br from-brand-500 to-brand-700 text-lg text-white"><ng-icon name="phosphorBus" /></span>
        {{ t.origin }} <span class="text-brand-500">→</span> {{ t.destination }}
      </h1>
      <p class="mb-6 text-slate-500 dark:text-slate-400">{{ 'booking.perSeat' | t: { price: (t.price | number: '1.0-0'), currency: t.currency } }}</p>

      @if (stops().length > 0) {
        <section class="card mb-6 p-5">
          <h2 class="mb-3 flex items-center gap-1.5 font-semibold text-slate-900 dark:text-slate-100">
            <ng-icon name="phosphorMapPinSimple" class="text-brand-600 dark:text-brand-400" /> {{ 'booking.route' | t }}
          </h2>
          <ol class="space-y-1.5 border-s-2 border-dashed border-slate-200 ps-4 text-sm text-slate-600 dark:border-white/10 dark:text-slate-300">
            <li class="flex justify-between font-medium text-slate-900 dark:text-slate-100"><span>{{ t.origin }}</span><span>{{ t.departureUtc | date: 'HH:mm' }}</span></li>
            @for (s of stops(); track s.sequence) {
              <li class="flex justify-between text-slate-500 dark:text-slate-400">
                <span>{{ s.name }}</span>
                <span>{{ s.arrivalUtc ? (s.arrivalUtc | date: 'HH:mm') : '—' }}</span>
              </li>
            }
            <li class="flex justify-between font-medium text-slate-900 dark:text-slate-100"><span>{{ t.destination }}</span><span>{{ t.arrivalUtc | date: 'HH:mm' }}</span></li>
          </ol>
        </section>
      }

      @if (reviews(); as r) {
        @if (r.count > 0) {
          <section class="card mb-6 p-5">
            <h2 class="mb-3 font-semibold text-slate-900 dark:text-slate-100">
              {{ 'booking.reviews' | t }}
              <span class="ms-1 text-sm font-normal text-amber-500">★ {{ r.averageRating | number: '1.1-1' }}</span>
              <span class="text-sm font-normal text-slate-400 dark:text-slate-500">({{ r.count }})</span>
            </h2>
            <ul class="stagger-children space-y-2">
              @for (rev of r.reviews; track rev.id) {
                <li class="text-sm">
                  <span class="text-amber-400">{{ stars(rev.rating) }}</span>
                  <span class="font-medium text-slate-700 dark:text-slate-300">{{ rev.displayName }}</span>
                  @if (rev.comment) { <span class="italic text-slate-500 dark:text-slate-400">“{{ rev.comment }}”</span> }
                </li>
              }
            </ul>
          </section>
        }
      }

      @if (!held()) {
        <section class="card p-5">
          <h2 class="mb-3 font-semibold text-slate-900 dark:text-slate-100">{{ 'booking.chooseSeats' | t }}</h2>
          <p class="mb-4 text-sm text-slate-500 dark:text-slate-400">{{ 'booking.seatHint' | t: { max: maxSeats } }}</p>
          <div class="space-y-2">
            @for (row of rows(); track $index) {
              <div class="flex justify-center gap-2">
                @for (seat of row; track seat) {
                  <button
                    type="button"
                    [disabled]="isTaken(seat)"
                    (click)="toggleSeat(seat)"
                    class="h-10 w-10 cursor-pointer rounded-lg border text-sm font-medium transition-all duration-150 enabled:hover:-translate-y-0.5 enabled:hover:shadow-sm disabled:cursor-not-allowed"
                    [class]="
                      isSelected(seat)
                        ? 'bg-brand-600 text-white border-brand-600 shadow-sm'
                        : isTaken(seat)
                          ? 'bg-slate-200 text-slate-400 dark:bg-white/10 dark:text-slate-600'
                          : 'border-slate-300 text-slate-700 dark:border-white/15 dark:text-slate-300'
                    "
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
            class="btn btn-primary mt-5"
          >
            {{ holding() ? ('booking.holding' | t) : ('booking.holdSeats' | t: { count: selectedSeats().length }) }}
          </button>
        </section>
      } @else {
        <section class="card p-5">
          <div class="mb-4 flex items-center justify-between">
            <h2 class="font-semibold text-slate-900 dark:text-slate-100">{{ 'booking.passengerDetails' | t }}</h2>
            <span
              class="badge"
              [class]="remaining() > 0 ? 'bg-amber-100 text-amber-700 dark:bg-amber-400/15 dark:text-amber-300' : 'bg-rose-100 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300'"
            >
              <ng-icon name="phosphorClockCountdown" />
              {{ remaining() > 0 ? ('booking.heldLeft' | t: { time: countdown() }) : ('booking.holdExpired' | t) }}
            </span>
          </div>

          <form [formGroup]="passengerForm" (ngSubmit)="confirm()" class="space-y-4">
            <div formArrayName="passengers" class="stagger-children space-y-4">
              @for (group of passengers.controls; track $index) {
                <div [formGroupName]="$index" class="rounded-2xl ring-1 ring-slate-200 dark:ring-white/10 p-4">
                  <p class="mb-3 text-sm font-semibold text-slate-700 dark:text-slate-300">{{ 'booking.seatLabel' | t: { seat: group.value.seatNumber } }}</p>
                  <div class="grid gap-3 sm:grid-cols-2">
                    <div>
                      <label for="firstName-{{ $index }}" class="label">{{ 'booking.firstName' | t }} <span class="text-rose-500">*</span></label>
                      <input id="firstName-{{ $index }}" type="text" required formControlName="firstName" class="input" />
                    </div>
                    <div>
                      <label for="lastName-{{ $index }}" class="label">{{ 'booking.lastName' | t }} <span class="text-rose-500">*</span></label>
                      <input id="lastName-{{ $index }}" type="text" required formControlName="lastName" class="input" />
                    </div>
                    <div>
                      <label for="documentType-{{ $index }}" class="label">{{ 'booking.idDocumentOptional' | t }}</label>
                      <select id="documentType-{{ $index }}" formControlName="documentType" class="input">
                        <option value="">{{ 'booking.idDocumentOptional' | t }}</option>
                        @for (dt of documentTypes; track dt) {
                          <option [value]="dt">{{ dt }}</option>
                        }
                      </select>
                    </div>
                    <div>
                      <label for="documentNumber-{{ $index }}" class="label">{{ 'booking.documentNumberOptional' | t }}</label>
                      <input id="documentNumber-{{ $index }}" type="text" formControlName="documentNumber" class="input" />
                    </div>
                  </div>
                  @if ((group.get('firstName')?.invalid && group.get('firstName')?.touched) || (group.get('lastName')?.invalid && group.get('lastName')?.touched)) {
                    <p class="field-error">{{ 'booking.enterPassengerNames' | t }}</p>
                  }
                </div>
              }
            </div>

            <div class="rounded-2xl ring-1 ring-slate-200 dark:ring-white/10 p-4">
              <label for="promo-code" class="label flex items-center gap-1.5"><ng-icon name="phosphorTag" class="text-brand-600 dark:text-brand-400" /> {{ 'booking.promoCode' | t }}</label>
              <div class="flex gap-2">
                <input
                  id="promo-code"
                  type="text"
                  [value]="promoCode()"
                  (input)="onPromoInput($event)"
                  [placeholder]="'booking.promoPlaceholder' | t"
                  class="input flex-1 uppercase"
                />
                <button
                  type="button"
                  [disabled]="!promoCode() || promoChecking()"
                  (click)="applyPromo()"
                  class="btn btn-dark shrink-0"
                >
                  {{ (promoChecking() ? 'booking.checking' : 'booking.apply') | t }}
                </button>
              </div>
              @if (promo(); as p) {
                <p class="mt-2 text-sm font-medium text-emerald-700 dark:text-emerald-400">
                  {{ 'booking.promoApplied' | t: { discount: (p.discount | number: '1.0-0'), currency: p.currency, code: p.code } }}
                </p>
              }
            </div>

            <div class="rounded-2xl ring-1 ring-slate-200 dark:ring-white/10 p-4">
              <label for="contact-phone" class="label flex items-center gap-1.5"><ng-icon name="phosphorPhone" class="text-brand-600 dark:text-brand-400" /> {{ 'booking.contactPhone' | t }}</label>
              <input
                id="contact-phone"
                type="tel"
                [value]="contactPhone()"
                (input)="onContactPhoneInput($event)"
                [placeholder]="'booking.contactPhonePlaceholder' | t"
                class="input"
              />
            </div>

            <div class="flex items-center justify-between border-t border-dashed border-slate-200 pt-4 dark:border-white/10">
              <span class="text-lg font-bold text-slate-900 dark:text-slate-100">
                {{ 'booking.total' | t: { amount: (total() | number: '1.0-0'), currency: t.currency } }}
              </span>
              <button
                type="submit"
                [disabled]="submitting() || remaining() === 0"
                class="btn btn-primary"
              >
                {{ (submitting() ? 'booking.creating' : 'booking.confirmContinue') | t }}
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
  private readonly i18n = inject(TranslationService);
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
  protected readonly contactPhone = signal('');

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
      this.toasts.info(this.i18n.t('booking.pickTripFirst'));
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
        this.toasts.info(this.i18n.t('booking.maxSeats', { max: MAX_SEATS }));
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

  protected onContactPhoneInput(event: Event): void {
    this.contactPhone.set((event.target as HTMLInputElement).value);
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
        this.toasts.success(this.i18n.t('booking.promoAppliedToast'));
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
      this.toasts.info(this.i18n.t('booking.enterPassengerNames'));
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
    this.api.createBooking(trip.id, passengers, this.promo()?.code ?? null, this.contactPhone().trim() || null).subscribe({
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
          this.toasts.info(this.i18n.t('booking.seatsJustTaken'));
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
