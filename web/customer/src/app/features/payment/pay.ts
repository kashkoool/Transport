import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorCheckCircle, phosphorLockKey, phosphorTicket, phosphorXCircle } from '@ng-icons/phosphor-icons/regular';
import { ApiService } from '../../core/api/api.service';
import { ToastService } from '../../core/toast/toast.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-pay',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorCheckCircle, phosphorLockKey, phosphorTicket, phosphorXCircle })],
  template: `
    <div class="mx-auto flex min-h-[60vh] max-w-md items-center">
      <div class="card animate-in w-full p-7">
        <h1 class="mb-2 flex items-center gap-2 text-2xl font-bold text-slate-900 dark:text-slate-100">
          <span class="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-linear-to-br from-brand-500 to-brand-700 text-lg text-white"><ng-icon name="phosphorTicket" /></span>
          {{ 'pay.title' | t }}
        </h1>

        @if (loading()) {
          <div class="space-y-3 py-2">
            <div class="h-4 w-40 animate-pulse rounded bg-slate-100 dark:bg-white/10"></div>
            <div class="h-12 w-full animate-pulse rounded-2xl bg-slate-100 dark:bg-white/10"></div>
          </div>
        } @else {
          <p class="mb-6 text-slate-500 dark:text-slate-400">
            {{ 'pay.booking' | t }} <span class="font-mono font-medium text-slate-700 dark:text-slate-300">{{ reference() }}</span>
          </p>

          @if (!checkoutStarted()) {
            <button type="button" [disabled]="busy()" (click)="startCheckout()" class="btn btn-primary w-full">
              {{ (busy() ? 'pay.starting' : 'pay.proceed') | t }}
            </button>
            <p class="mt-3 flex items-center justify-center gap-1.5 text-center text-xs text-slate-400 dark:text-slate-500">
              <ng-icon name="phosphorLockKey" /> {{ 'pay.secureNote' | t }}
            </p>
          } @else {
            <!-- Development stand-in for the gateway's hosted page (no real card entry). -->
            <div class="rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-4 dark:border-white/15 dark:bg-white/5">
              <p class="mb-4 text-sm text-slate-600 dark:text-slate-300">
                {{ 'pay.sandboxNote' | t }}
              </p>
              <div class="flex gap-3">
                <button type="button" [disabled]="busy()" (click)="simulate(true)" class="btn btn-primary flex-1">
                  <ng-icon name="phosphorCheckCircle" /> {{ 'pay.payNow' | t }}
                </button>
                <button type="button" [disabled]="busy()" (click)="simulate(false)" class="btn btn-ghost flex-1">
                  <ng-icon name="phosphorXCircle" /> {{ 'pay.cancelPayment' | t }}
                </button>
              </div>
            </div>
          }
        }
      </div>
    </div>
  `,
})
export class PayComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly i18n = inject(TranslationService);

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
            this.toasts.error(this.i18n.t('pay.invalidUrl'));
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
          this.toasts.success(this.i18n.t('pay.confirmed'));
          this.router.navigate(['/ticket', this.bookingId]);
        } else {
          this.toasts.info(this.i18n.t('pay.notCompleted'));
          this.checkoutStarted.set(false);
        }
      },
      error: () => this.busy.set(false),
    });
  }
}
