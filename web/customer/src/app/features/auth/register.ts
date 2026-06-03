import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="mx-auto max-w-sm">
      <h1 class="mb-6 text-2xl font-bold text-slate-900">Create your account</h1>
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
        <div>
          <label for="fullName" class="mb-1 block text-sm font-medium text-slate-700">Full name</label>
          <input
            id="fullName"
            type="text"
            formControlName="fullName"
            autocomplete="name"
            class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          />
        </div>
        <div>
          <label for="email" class="mb-1 block text-sm font-medium text-slate-700">Email</label>
          <input
            id="email"
            type="email"
            formControlName="email"
            autocomplete="email"
            class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          />
        </div>
        <div>
          <label for="password" class="mb-1 block text-sm font-medium text-slate-700">Password</label>
          <input
            id="password"
            type="password"
            formControlName="password"
            autocomplete="new-password"
            class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          />
          <p class="mt-1 text-xs text-slate-500">At least 10 characters including a symbol.</p>
        </div>
        <button
          type="submit"
          [disabled]="submitting()"
          class="w-full rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
        >
          {{ submitting() ? 'Creating…' : 'Create account' }}
        </button>
      </form>
      <div class="my-4 flex items-center gap-3 text-xs text-slate-400">
        <span class="h-px flex-1 bg-slate-200"></span>OR<span class="h-px flex-1 bg-slate-200"></span>
      </div>
      <button
        type="button"
        (click)="googleSignIn()"
        class="flex w-full items-center justify-center gap-2 rounded-md border border-slate-300 px-4 py-2 font-medium text-slate-700 hover:bg-slate-50"
      >
        <span class="font-bold text-indigo-600">G</span> Sign up with Google
      </button>
      <p class="mt-4 text-center text-sm text-slate-600">
        Already have an account?
        <a routerLink="/login" class="font-medium text-indigo-600 hover:text-indigo-700">Sign in</a>
      </p>
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
