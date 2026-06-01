import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnDestroy,
  signal,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { BookingFlow } from '../../core/booking-flow';
import { ToastService } from '../../core/toast/toast.service';
import { HoldResult } from '../../core/models';

const MAX_SEATS = 10;

@Component({
  selector: 'app-booking',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DecimalPipe],
  template: `
    @if (trip(); as t) {
      <button type="button" class="text-sm text-indigo-600 hover:text-indigo-700" (click)="back()">
        ← Back to search
      </button>
      <h1 class="mt-2 text-2xl font-bold text-slate-900">{{ t.origin }} → {{ t.destination }}</h1>
      <p class="mb-6 text-slate-500">{{ t.price | number: '1.0-0' }} {{ t.currency }} per seat</p>

      @if (!held()) {
        <section class="rounded-xl border border-slate-200 bg-white p-4">
          <h2 class="mb-3 font-semibold text-slate-900">Choose your seats</h2>
          <p class="mb-3 text-sm text-slate-500">Pick up to {{ maxSeats }} seats.</p>
          <div class="grid grid-cols-6 gap-2 sm:grid-cols-10">
            @for (seat of seats(); track seat) {
              <button
                type="button"
                (click)="toggleSeat(seat)"
                class="aspect-square rounded-md border text-sm font-medium"
                [class.bg-indigo-600]="isSelected(seat)"
                [class.text-white]="isSelected(seat)"
                [class.border-indigo-600]="isSelected(seat)"
                [class.border-slate-300]="!isSelected(seat)"
                [class.text-slate-700]="!isSelected(seat)"
              >
                {{ seat }}
              </button>
            }
          </div>
          <button
            type="button"
            [disabled]="selectedSeats().length === 0 || holding()"
            (click)="hold()"
            class="mt-4 rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
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
              {{ remaining() > 0 ? 'Held — ' + countdown() + ' left' : 'Hold expired' }}
            </span>
          </div>

          <form [formGroup]="passengerForm" (ngSubmit)="confirm()" class="space-y-4">
            <div formArrayName="passengers" class="space-y-4">
              @for (group of passengers.controls; track $index) {
                <div [formGroupName]="$index" class="grid gap-3 sm:grid-cols-3">
                  <div class="flex items-center text-sm font-medium text-slate-700">
                    Seat {{ group.value.seatNumber }}
                  </div>
                  <input
                    type="text"
                    formControlName="firstName"
                    placeholder="First name"
                    class="rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                  <input
                    type="text"
                    formControlName="lastName"
                    placeholder="Last name"
                    class="rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>
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
export class BookingComponent implements OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly toasts = inject(ToastService);
  private readonly router = inject(Router);
  protected readonly flow = inject(BookingFlow);

  protected readonly maxSeats = MAX_SEATS;
  protected readonly trip = this.flow.trip;
  protected readonly selectedSeats = signal<number[]>([]);
  protected readonly held = signal(false);
  protected readonly holding = signal(false);
  protected readonly submitting = signal(false);
  protected readonly remaining = signal(0); // seconds until the hold expires
  protected readonly total = computed(() => (this.trip()?.price ?? 0) * this.selectedSeats().length);

  protected readonly passengers = new FormArray<FormGroup>([]);
  protected readonly passengerForm = this.fb.group({ passengers: this.passengers });

  private timer?: ReturnType<typeof setInterval>;

  constructor() {
    if (!this.trip()) {
      this.toasts.info('Please pick a trip first.');
      this.router.navigate(['/search']);
    }
  }

  protected seats(): number[] {
    const count = this.trip()?.seatCount ?? 0;
    return Array.from({ length: count }, (_, i) => i + 1);
  }

  protected isSelected(seat: number): boolean {
    return this.selectedSeats().includes(seat);
  }

  protected toggleSeat(seat: number): void {
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
      error: () => this.holding.set(false), // 409 etc. surfaces as a toast; user can reselect
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
    }[];
    this.api.createBooking(trip.id, passengers).subscribe({
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

  private buildPassengerForms(): void {
    this.passengers.clear();
    for (const seat of this.selectedSeats()) {
      this.passengers.push(
        this.fb.nonNullable.group({
          firstName: ['', [Validators.required, Validators.maxLength(100)]],
          lastName: ['', [Validators.required, Validators.maxLength(100)]],
          seatNumber: [seat],
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
  }
}
