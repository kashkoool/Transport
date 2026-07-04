import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/toast/toast.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-reset-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe],
  template: `
    <div class="mx-auto max-w-sm">
      <h1 class="mb-6 text-2xl font-bold text-slate-900 dark:text-slate-100">{{ 'auth.reset.title' | t }}</h1>
      @if (!hasLink()) {
        <div class="rounded-md bg-amber-50 p-4 text-sm text-amber-800">
          {{ 'auth.reset.incomplete' | t }}
          <a routerLink="/forgot-password" class="font-medium underline">{{ 'auth.forgot.title' | t }}</a>
        </div>
      } @else {
        <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
          <div>
            <label for="password" class="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">{{ 'auth.reset.newPassword' | t }}</label>
            <input
              id="password"
              type="password"
              formControlName="password"
              autocomplete="new-password"
              class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
            />
            <p class="mt-1 text-xs text-slate-500 dark:text-slate-400">{{ 'auth.passwordHint' | t }}</p>
          </div>
          <button
            type="submit"
            [disabled]="submitting()"
            class="w-full rounded-md bg-brand-600 px-4 py-2 font-medium text-white hover:bg-brand-700 disabled:opacity-50"
          >
            {{ (submitting() ? 'auth.reset.saving' : 'auth.reset.save') | t }}
          </button>
        </form>
      }
      <p class="mt-4 text-center text-sm text-slate-600 dark:text-slate-400">
        <a routerLink="/login" class="font-medium text-brand-600 hover:text-brand-700">{{ 'auth.backToSignin' | t }}</a>
      </p>
    </div>
  `,
})
export class ResetPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly toasts = inject(ToastService);

  // The email + token are carried in the reset link the user clicked.
  private readonly email = this.route.snapshot.queryParamMap.get('email') ?? '';
  private readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';

  protected readonly submitting = signal(false);
  protected readonly hasLink = signal(this.email.length > 0 && this.token.length > 0);
  protected readonly form = this.fb.nonNullable.group({
    // Mirror the server policy: min 10 chars and at least one non-alphanumeric character.
    password: ['', [Validators.required, Validators.pattern(/^(?=.*[^a-zA-Z0-9]).{10,}$/)]],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.auth.resetPassword(this.email, this.token, this.form.getRawValue().password).subscribe({
      next: () => {
        this.toasts.success('Your password has been reset. Please sign in.');
        this.router.navigateByUrl('/login');
      },
      error: () => this.submitting.set(false),
    });
  }
}
