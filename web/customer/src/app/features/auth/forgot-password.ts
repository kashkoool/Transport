import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorCheckCircle, phosphorEnvelopeSimple } from '@ng-icons/phosphor-icons/regular';
import { AuthService } from '../../core/auth/auth.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-forgot-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorCheckCircle, phosphorEnvelopeSimple })],
  template: `
    <div class="mx-auto flex min-h-[60vh] max-w-sm items-center">
      <div class="card animate-in w-full p-7">
        <h1 class="mb-2 flex items-center gap-2 text-2xl font-bold text-slate-900 dark:text-slate-100">
          <span class="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-linear-to-br from-brand-500 to-brand-700 text-lg text-white"><ng-icon name="phosphorEnvelopeSimple" /></span>
          {{ 'auth.forgot.title' | t }}
        </h1>
        <p class="mb-6 text-sm text-slate-600 dark:text-slate-400">
          {{ 'auth.forgot.subtitle' | t }}
        </p>
        @if (sent()) {
          <div class="flex items-start gap-2 rounded-2xl bg-emerald-50 p-4 text-sm text-emerald-800 ring-1 ring-emerald-100 dark:bg-emerald-500/10 dark:text-emerald-300 dark:ring-emerald-500/20">
            <ng-icon name="phosphorCheckCircle" class="mt-0.5 shrink-0 text-base" />
            <span>{{ 'auth.forgot.sent' | t }}</span>
          </div>
        } @else {
          <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
            <div>
              <label for="email" class="label">{{ 'auth.email' | t }}</label>
              <input id="email" type="email" formControlName="email" autocomplete="email" class="input" />
            </div>
            <button type="submit" [disabled]="submitting()" class="btn btn-primary w-full">
              {{ (submitting() ? 'auth.forgot.sending' : 'auth.forgot.send') | t }}
            </button>
          </form>
        }
        <p class="mt-5 text-center text-sm text-slate-600 dark:text-slate-400">
          {{ 'auth.forgot.remembered' | t }}
          <a routerLink="/login" class="font-semibold text-brand-600 transition hover:text-brand-700 dark:text-brand-400 dark:hover:text-brand-300">{{ 'auth.backToSignin' | t }}</a>
        </p>
      </div>
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
