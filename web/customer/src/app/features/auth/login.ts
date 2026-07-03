import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/toast/toast.service';

@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="mx-auto flex min-h-[70vh] max-w-md items-center">
      <div class="card w-full overflow-hidden">
        <!-- Branded header strip -->
        <div class="relative overflow-hidden bg-linear-to-br from-brand-700 to-brand-500 px-7 py-7 text-white">
          <div class="pointer-events-none absolute -right-8 -top-8 h-32 w-32 rounded-full bg-white/10 blur-2xl"></div>
          <p class="text-sm tracking-[0.3em] text-flag">★★★</p>
          <h1 class="mt-1.5 text-2xl font-bold">Welcome back</h1>
          <p class="mt-1 text-sm text-white/80">Sign in to book and manage your trips.</p>
        </div>

        <div class="p-7">
          <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
            <div>
              <label for="email" class="label">Email</label>
              <input id="email" type="email" formControlName="email" autocomplete="email" class="input" />
            </div>
            <div>
              <label for="password" class="label">Password</label>
              <input id="password" type="password" formControlName="password" autocomplete="current-password" class="input" />
            </div>
            <button type="submit" [disabled]="submitting()" class="btn btn-primary w-full">
              {{ submitting() ? 'Signing in…' : 'Sign in' }}
            </button>
          </form>

          <div class="my-5 flex items-center gap-3 text-xs font-medium text-slate-400 dark:text-slate-500">
            <span class="h-px flex-1 bg-slate-200 dark:bg-white/10"></span>OR<span class="h-px flex-1 bg-slate-200 dark:bg-white/10"></span>
          </div>

          <button type="button" (click)="googleSignIn()" class="btn btn-ghost w-full">
            <span class="font-bold text-brand-600 dark:text-brand-400">G</span> Continue with Google
          </button>

          <p class="mt-5 text-center text-sm">
            <a routerLink="/forgot-password" class="font-semibold text-brand-600 hover:text-brand-700 dark:text-brand-400">Forgot password?</a>
          </p>
          <p class="mt-2 text-center text-sm text-slate-600 dark:text-slate-400">
            New here?
            <a routerLink="/register" class="font-semibold text-brand-600 hover:text-brand-700 dark:text-brand-400">Create an account</a>
          </p>
        </div>
      </div>
    </div>
  `,
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly toasts = inject(ToastService);

  protected readonly submitting = signal(false);
  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  constructor() {
    // Surface failures bounced back from the Google OAuth callback.
    const error = this.route.snapshot.queryParamMap.get('error');
    if (error === 'google') {
      this.toasts.error('Google sign-in failed. Please try again.');
    } else if (error === 'google_link') {
      this.toasts.error('That email already has an account. Sign in with your password first, then link Google.');
    }
  }

  /** Full-page redirect into the Google flow, preserving any returnUrl. */
  protected googleSignIn(): void {
    this.auth.loginWithGoogle(this.route.snapshot.queryParamMap.get('returnUrl') ?? undefined);
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => {
        // Honor an explicit returnUrl (e.g. bounced from a guarded page), else send the user to
        // the home for their role (admin → companies, vendor → trips, customer → search).
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        this.router.navigateByUrl(returnUrl ?? this.auth.homeRoute());
      },
      error: () => this.submitting.set(false),
    });
  }
}
