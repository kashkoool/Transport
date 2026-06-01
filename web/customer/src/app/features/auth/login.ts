import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="mx-auto max-w-sm">
      <h1 class="mb-6 text-2xl font-bold text-slate-900">Welcome back</h1>
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
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
            autocomplete="current-password"
            class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          />
        </div>
        <button
          type="submit"
          [disabled]="submitting()"
          class="w-full rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
        >
          {{ submitting() ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>
      <p class="mt-4 text-center text-sm text-slate-600">
        New here?
        <a routerLink="/register" class="font-medium text-indigo-600 hover:text-indigo-700">Create an account</a>
      </p>
    </div>
  `,
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly submitting = signal(false);
  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

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
