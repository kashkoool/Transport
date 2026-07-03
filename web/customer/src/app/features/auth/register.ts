import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="mx-auto flex min-h-[70vh] max-w-md items-center">
      <div class="card w-full overflow-hidden">
        <!-- Branded header strip -->
        <div class="relative overflow-hidden bg-linear-to-br from-brand-700 to-brand-500 px-7 py-7 text-white">
          <div class="pointer-events-none absolute -right-8 -top-8 h-32 w-32 rounded-full bg-white/10 blur-2xl"></div>
          <p class="text-sm tracking-[0.3em] text-flag">★★★</p>
          <h1 class="mt-1.5 text-2xl font-bold">Create your account</h1>
          <p class="mt-1 text-sm text-white/80">Book across every Syrian operator in one place.</p>
        </div>

        <div class="p-7">
          <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
            <div>
              <label for="fullName" class="label">Full name</label>
              <input id="fullName" type="text" formControlName="fullName" autocomplete="name" class="input" />
            </div>
            <div>
              <label for="email" class="label">Email</label>
              <input id="email" type="email" formControlName="email" autocomplete="email" class="input" />
            </div>
            <div>
              <label for="password" class="label">Password</label>
              <input id="password" type="password" formControlName="password" autocomplete="new-password" class="input" />
              <p class="mt-1.5 text-xs text-slate-500 dark:text-slate-400">At least 10 characters including a symbol.</p>
            </div>
            <button type="submit" [disabled]="submitting()" class="btn btn-primary w-full">
              {{ submitting() ? 'Creating…' : 'Create account' }}
            </button>
          </form>

          <div class="my-5 flex items-center gap-3 text-xs font-medium text-slate-400 dark:text-slate-500">
            <span class="h-px flex-1 bg-slate-200 dark:bg-white/10"></span>OR<span class="h-px flex-1 bg-slate-200 dark:bg-white/10"></span>
          </div>

          <button type="button" (click)="googleSignIn()" class="btn btn-ghost w-full">
            <span class="font-bold text-brand-600 dark:text-brand-400">G</span> Sign up with Google
          </button>

          <p class="mt-5 text-center text-sm text-slate-600 dark:text-slate-400">
            Already have an account?
            <a routerLink="/login" class="font-semibold text-brand-600 hover:text-brand-700 dark:text-brand-400">Sign in</a>
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
    this.auth.register(email, password, fullName).subscribe({
      next: () => this.router.navigateByUrl('/search'),
      error: () => this.submitting.set(false),
    });
  }
}
