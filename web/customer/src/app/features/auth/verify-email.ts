import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorCheckCircle, phosphorCircleNotch, phosphorWarningCircle, phosphorXCircle } from '@ng-icons/phosphor-icons/regular';
import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/toast/toast.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

type VerifyState = 'verifying' | 'success' | 'failure' | 'invalid';

@Component({
  selector: 'app-verify-email',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorCheckCircle, phosphorCircleNotch, phosphorWarningCircle, phosphorXCircle })],
  template: `
    <div class="mx-auto flex min-h-[60vh] max-w-sm items-center">
      <div class="card animate-in w-full p-7 text-center">
        @switch (state()) {
          @case ('verifying') {
            <ng-icon name="phosphorCircleNotch" class="mx-auto block animate-spin text-4xl text-brand-500" />
            <p class="mt-4 text-sm text-slate-600 dark:text-slate-400">{{ 'auth.verify.verifying' | t }}</p>
          }
          @case ('success') {
            <div class="flex items-start gap-2 rounded-2xl bg-emerald-50 p-4 text-start text-sm text-emerald-800 ring-1 ring-emerald-100 dark:bg-emerald-500/10 dark:text-emerald-300 dark:ring-emerald-500/20">
              <ng-icon name="phosphorCheckCircle" class="mt-0.5 shrink-0 text-base" />
              <span>{{ 'auth.verify.success' | t }}</span>
            </div>
            <a routerLink="/login" class="btn btn-primary mt-5 w-full">{{ 'auth.verify.continue' | t }}</a>
          }
          @case ('invalid') {
            <div class="flex items-start gap-2 rounded-2xl bg-amber-50 p-4 text-start text-sm text-amber-800 ring-1 ring-amber-100 dark:bg-amber-400/10 dark:text-amber-300 dark:ring-amber-400/20">
              <ng-icon name="phosphorWarningCircle" class="mt-0.5 shrink-0 text-base" />
              <span>{{ 'auth.verify.failed' | t }}</span>
            </div>
          }
          @case ('failure') {
            <div class="flex items-start gap-2 rounded-2xl bg-rose-50 p-4 text-start text-sm text-rose-800 ring-1 ring-rose-100 dark:bg-rose-500/10 dark:text-rose-300 dark:ring-rose-500/20">
              <ng-icon name="phosphorXCircle" class="mt-0.5 shrink-0 text-base" />
              <span>{{ 'auth.verify.failed' | t }}</span>
            </div>
            <button type="button" (click)="resend()" [disabled]="resending()" class="btn btn-soft mt-5 w-full">
              {{ (resending() ? 'auth.verify.resending' : 'auth.verify.resend') | t }}
            </button>
          }
        }
      </div>
    </div>
  `,
})
export class VerifyEmailComponent {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly toasts = inject(ToastService);

  private readonly email = this.route.snapshot.queryParamMap.get('email') ?? '';
  private readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';

  protected readonly state = signal<VerifyState>('verifying');
  protected readonly resending = signal(false);

  constructor() {
    if (this.email.length === 0 || this.token.length === 0) {
      this.state.set('invalid');
      return;
    }
    this.auth.verifyEmail(this.email, this.token).subscribe({
      next: () => this.state.set('success'),
      error: () => this.state.set('failure'),
    });
  }

  protected resend(): void {
    if (this.email.length === 0) return;
    this.resending.set(true);
    this.auth.resendVerification(this.email).subscribe({
      next: () => {
        this.toasts.success('If your account needs verifying, a new link is on its way.');
        this.resending.set(false);
      },
      error: () => this.resending.set(false),
    });
  }
}
