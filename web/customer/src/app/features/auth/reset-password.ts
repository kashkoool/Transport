import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorLockKey, phosphorWarningCircle } from '@ng-icons/phosphor-icons/regular';
import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/toast/toast.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-reset-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorLockKey, phosphorWarningCircle })],
  template: `
    <div class="mx-auto flex min-h-[60vh] max-w-sm items-center">
      <div class="card animate-in w-full p-7">
        <h1 class="mb-6 flex items-center gap-2 text-2xl font-bold text-slate-900 dark:text-slate-100">
          <span class="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-linear-to-br from-brand-500 to-brand-700 text-lg text-white"><ng-icon name="phosphorLockKey" /></span>
          {{ 'auth.reset.title' | t }}
        </h1>
        @if (!hasLink()) {
          <div class="flex items-start gap-2 rounded-2xl bg-amber-50 p-4 text-sm text-amber-800 ring-1 ring-amber-100 dark:bg-amber-400/10 dark:text-amber-300 dark:ring-amber-400/20">
            <ng-icon name="phosphorWarningCircle" class="mt-0.5 shrink-0 text-base" />
            <span>
              {{ 'auth.reset.incomplete' | t }}
              <a routerLink="/forgot-password" class="font-semibold underline">{{ 'auth.forgot.title' | t }}</a>
            </span>
          </div>
        } @else {
          <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
            <div>
              <label for="password" class="label">{{ 'auth.reset.newPassword' | t }}</label>
              <input id="password" type="password" formControlName="password" autocomplete="new-password" class="input" />
              <p class="mt-1.5 text-xs text-slate-500 dark:text-slate-400">{{ 'auth.passwordHint' | t }}</p>
            </div>
            <button type="submit" [disabled]="submitting()" class="btn btn-primary w-full">
              {{ (submitting() ? 'auth.reset.saving' : 'auth.reset.save') | t }}
            </button>
          </form>
        }
        <p class="mt-5 text-center text-sm text-slate-600 dark:text-slate-400">
          <a routerLink="/login" class="font-semibold text-brand-600 transition hover:text-brand-700 dark:text-brand-400 dark:hover:text-brand-300">{{ 'auth.backToSignin' | t }}</a>
        </p>
      </div>
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
