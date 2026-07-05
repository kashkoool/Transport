import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe],
  template: `
    <div class="mx-auto flex min-h-[70vh] max-w-md items-center">
      <div class="card w-full overflow-hidden">
        <!-- Branded header strip -->
        <div class="relative overflow-hidden bg-linear-to-br from-brand-700 to-brand-500 px-7 py-7 text-white">
          <div class="pointer-events-none absolute -right-8 -top-8 h-32 w-32 rounded-full bg-white/10 blur-2xl"></div>
          <p class="text-sm tracking-[0.3em] text-flag">★★★</p>
          <h1 class="mt-1.5 text-2xl font-bold">{{ 'auth.register.title' | t }}</h1>
          <p class="mt-1 text-sm text-white/80">{{ 'auth.register.subtitle' | t }}</p>
        </div>

        <div class="p-7">
          <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
            <div>
              <label for="fullName" class="label">{{ 'auth.fullName' | t }}</label>
              <input id="fullName" type="text" formControlName="fullName" autocomplete="name" class="input" />
            </div>
            <div>
              <label for="email" class="label">{{ 'auth.email' | t }}</label>
              <input id="email" type="email" formControlName="email" autocomplete="email" class="input" />
            </div>
            <div>
              <label for="password" class="label">{{ 'auth.password' | t }}</label>
              <input id="password" type="password" formControlName="password" autocomplete="new-password" class="input" />
              <p class="mt-1.5 text-xs text-slate-500 dark:text-slate-400">{{ 'auth.passwordHint' | t }}</p>
            </div>
            <button type="submit" [disabled]="submitting()" class="btn btn-primary w-full">
              {{ (submitting() ? 'auth.creating' : 'auth.createAccount') | t }}
            </button>
          </form>

          <div class="my-5 flex items-center gap-3 text-xs font-medium text-slate-400 dark:text-slate-500">
            <span class="h-px flex-1 bg-slate-200 dark:bg-white/10"></span>{{ 'common.or' | t }}<span class="h-px flex-1 bg-slate-200 dark:bg-white/10"></span>
          </div>

          <button type="button" (click)="googleSignIn()" class="btn btn-ghost w-full">
            <span class="font-bold text-brand-600 dark:text-brand-400">G</span> {{ 'auth.googleSignup' | t }}
          </button>

          <p class="mt-5 text-center text-sm text-slate-600 dark:text-slate-400">
            {{ 'auth.haveAccount' | t }}
            <a routerLink="/login" class="font-semibold text-brand-600 hover:text-brand-700 dark:text-brand-400">{{ 'auth.signin' | t }}</a>
          </p>
        </div>
      </div>
    </div>
  `,
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly i18n = inject(TranslationService);

  protected readonly submitting = signal(false);
  protected readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    // Mirror the server policy: min 10 chars and at least one non-alphanumeric character.
    password: ['', [Validators.required, Validators.pattern(/^(?=.*[^a-zA-Z0-9]).{10,}$/)]],
  });

  /** Full-page redirect into the Google flow for sign-up. */
  protected googleSignIn(): void {
    this.auth.loginWithGoogle();
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    const { email, password, fullName } = this.form.getRawValue();
    // Send the current UI language so the verification email matches what the user is reading.
    this.auth.register(email, password, fullName, this.i18n.lang()).subscribe({
      next: () => this.router.navigateByUrl('/search'),
      error: () => this.submitting.set(false),
    });
  }
}
