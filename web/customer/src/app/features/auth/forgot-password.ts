import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-forgot-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="mx-auto max-w-sm">
      <h1 class="mb-2 text-2xl font-bold text-slate-900">Reset your password</h1>
      <p class="mb-6 text-sm text-slate-600">
        Enter your email and we'll send you a link to set a new password.
      </p>
      @if (sent()) {
        <div class="rounded-md bg-green-50 p-4 text-sm text-green-800">
          If an account exists for that email, a reset link is on its way. Check your inbox.
        </div>
      } @else {
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
          <button
            type="submit"
            [disabled]="submitting()"
            class="w-full rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
          >
            {{ submitting() ? 'Sending…' : 'Send reset link' }}
          </button>
        </form>
      }
      <p class="mt-4 text-center text-sm text-slate-600">
        Remembered it?
        <a routerLink="/login" class="font-medium text-indigo-600 hover:text-indigo-700">Back to sign in</a>
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
