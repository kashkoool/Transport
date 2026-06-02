import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/toast/toast.service';

type VerifyState = 'verifying' | 'success' | 'failure' | 'invalid';

@Component({
  selector: 'app-verify-email',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="mx-auto max-w-sm text-center">
      <h1 class="mb-6 text-2xl font-bold text-slate-900">Email verification</h1>
      @switch (state()) {
        @case ('verifying') {
          <p class="text-sm text-slate-600">Verifying your email…</p>
        }
        @case ('success') {
          <div class="rounded-md bg-green-50 p-4 text-sm text-green-800">
            Your email is verified. You're all set.
          </div>
          <a
            routerLink="/login"
            class="mt-4 inline-block rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700"
            >Continue to sign in</a
          >
        }
        @case ('invalid') {
          <div class="rounded-md bg-amber-50 p-4 text-sm text-amber-800">
            This verification link is incomplete.
          </div>
        }
        @case ('failure') {
          <div class="rounded-md bg-red-50 p-4 text-sm text-red-800">
            This verification link is invalid or has expired.
          </div>
          <button
            type="button"
            (click)="resend()"
            [disabled]="resending()"
            class="mt-4 rounded-md border border-indigo-600 px-4 py-2 font-medium text-indigo-600 hover:bg-indigo-50 disabled:opacity-50"
          >
            {{ resending() ? 'Sending…' : 'Send a new link' }}
          </button>
        }
      }
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
