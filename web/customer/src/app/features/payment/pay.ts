import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { ToastService } from '../../core/toast/toast.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-pay',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-md">
      <h1 class="mb-2 text-2xl font-bold text-slate-900">Payment</h1>

      @if (loading()) {
        <p class="text-slate-500">Loading…</p>
      } @else {
        <p class="mb-6 text-slate-500">
          Booking <span class="font-mono font-medium text-slate-700">{{ reference() }}</span>
        </p>

        @if (!checkoutStarted()) {
          <button
            type="button"
            [disabled]="busy()"
            (click)="startCheckout()"
            class="w-full rounded-md bg-brand-600 px-4 py-3 font-medium text-white hover:bg-brand-700 disabled:opacity-50"
          >
            {{ busy() ? 'Starting…' : 'Proceed to payment' }}
          </button>
          <p class="mt-3 text-center text-xs text-slate-400">
            You'll be taken to the payment provider's secure page. No card details are stored by TPX.
          </p>
        } @else {
          <!-- Development stand-in for the gateway's hosted page (no real card entry). -->
          <div class="rounded-xl border border-dashed border-slate-300 bg-white p-4">
            <p class="mb-4 text-sm text-slate-600">
              Sandbox gateway. Simulate the payment result the provider would send back:
            </p>
            <div class="flex gap-3">
              <button
                type="button"
                [disabled]="busy()"
                (click)="simulate(true)"
                class="flex-1 rounded-md bg-emerald-600 px-4 py-2 font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
              >
                Pay now
              </button>
              <button
                type="button"
                [disabled]="busy()"
                (click)="simulate(false)"
                class="flex-1 rounded-md bg-slate-200 px-4 py-2 font-medium text-slate-700 hover:bg-slate-300 disabled:opacity-50"
              >
                Cancel payment
              </button>
            </div>
          </div>
        }
      }
    </div>
  `,
})
export class PayComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);

  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly checkoutStarted = signal(false);
  protected readonly reference = signal('');

  private bookingId = '';

  ngOnInit(): void {
    this.bookingId = this.route.snapshot.paramMap.get('bookingId') ?? '';
    // The ticket endpoint works for a pending booking too — use it to get the reference and to
    // short-circuit to the ticket if this booking is already paid.
    this.api.ticket(this.bookingId).subscribe({
      next: (ticket) => {
        this.reference.set(ticket.reference);
        this.loading.set(false);
        if (ticket.status === 'Confirmed') {
          this.router.navigate(['/ticket', this.bookingId]);
        }
      },
      error: () => {
        this.loading.set(false);
        this.router.navigate(['/my-bookings']);
      },
    });
  }

  protected startCheckout(): void {
    this.busy.set(true);
    this.api.checkout(this.bookingId).subscribe({
      next: (session) => {
        this.busy.set(false);
        if (environment.production) {
          // Hand off to the gateway's hosted checkout page. Defense-in-depth: only ever navigate
          // to an absolute https URL, so a malformed/non-https value can never become a
          // javascript:/data: redirect.
          if (/^https:\/\//i.test(session.checkoutUrl)) {
            window.location.href = session.checkoutUrl;
          } else {
            this.toasts.error('The payment provider returned an invalid checkout URL.');
          }
        } else {
          // Local/dev: the sandbox URL isn't a real page, so reveal the simulate controls.
          this.checkoutStarted.set(true);
        }
      },
      error: () => this.busy.set(false),
    });
  }

  protected simulate(succeeded: boolean): void {
    this.busy.set(true);
    this.api.simulatePayment(this.reference(), succeeded).subscribe({
      next: (result) => {
        this.busy.set(false);
        if (result.status === 'confirmed') {
          this.toasts.success('Payment confirmed!');
          this.router.navigate(['/ticket', this.bookingId]);
        } else {
          this.toasts.info('Payment was not completed.');
          this.checkoutStarted.set(false);
        }
      },
      error: () => this.busy.set(false),
    });
  }
}
