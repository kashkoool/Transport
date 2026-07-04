import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-forgot-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe],
  template: `
    <div class="mx-auto max-w-sm">
      <h1 class="mb-2 text-2xl font-bold text-slate-900 dark:text-slate-100">{{ 'auth.forgot.title' | t }}</h1>
      <p class="mb-6 text-sm text-slate-600 dark:text-slate-400">
        {{ 'auth.forgot.subtitle' | t }}
      </p>
      @if (sent()) {
        <div class="rounded-md bg-green-50 p-4 text-sm text-green-800">
          {{ 'auth.forgot.sent' | t }}
        </div>
      } @else {
        <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
          <div>
            <label for="email" class="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">{{ 'auth.email' | t }}</label>
            <input
              id="email"
              type="email"
              formControlName="email"
              autocomplete="email"
              class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
            />
          </div>
          <button
            type="submit"
            [disabled]="submitting()"
            class="w-full rounded-md bg-brand-600 px-4 py-2 font-medium text-white hover:bg-brand-700 disabled:opacity-50"
          >
            {{ (submitting() ? 'auth.forgot.sending' : 'auth.forgot.send') | t }}
          </button>
        </form>
      }
      <p class="mt-4 text-center text-sm text-slate-600 dark:text-slate-400">
        {{ 'auth.forgot.remembered' | t }}
        <a routerLink="/login" class="font-medium text-brand-600 hover:text-brand-700">{{ 'auth.backToSignin' | t }}</a>
      </p>
    </div>
  `,
})
export class ForgotPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);

  protected readonly submitting = signal(false);
  protected readonly sent = signal(false);
  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.auth.forgotPassword(this.form.getRawValue().email).subscribe({
      next: () => {
        this.sent.set(true);
        this.submitting.set(false);
      },
      error: () => this.submitting.set(false),
    });
  }
}
