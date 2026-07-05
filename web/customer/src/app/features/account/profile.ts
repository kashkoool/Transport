import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/toast/toast.service';
import { UserProfile } from '../../core/models';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

// Same policy the register/manager forms use: ≥10 chars with upper, lower, digit and a symbol.
const STRONG_PASSWORD = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{10,}$/;

@Component({
  selector: 'app-profile',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, TranslatePipe],
  template: `
    <h1 class="mb-6 text-2xl font-bold text-slate-900">{{ 'account.title' | t }}</h1>

    @if (loading()) {
      <p class="text-slate-500">{{ 'common.loading' | t }}</p>
    } @else if (loadFailed()) {
      <div class="rounded-xl bg-slate-100 p-6 text-center">
        <p class="text-slate-600">{{ 'account.loadFailed' | t }}</p>
        <button type="button" (click)="reload()" class="mt-2 font-medium text-brand-600 hover:text-brand-700">{{ 'account.tryAgain' | t }}</button>
      </div>
    } @else if (profile(); as p) {
      <div class="grid gap-6 lg:grid-cols-2">
        <section class="rounded-xl border border-slate-200 bg-white p-5">
          <h2 class="mb-3 font-semibold text-slate-900">{{ 'account.profile' | t }}</h2>
          <p class="mb-3 text-sm text-slate-500">{{ 'account.signedInAs' | t }} <span class="font-medium text-slate-700">{{ p.email }}</span></p>
          <form [formGroup]="profileForm" (ngSubmit)="saveProfile()" class="space-y-3">
            <div>
              <label for="fullName" class="mb-1 block text-sm font-medium text-slate-700">{{ 'account.fullName' | t }}</label>
              <input id="fullName" type="text" formControlName="fullName" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
            </div>
            <div>
              <label for="phone" class="mb-1 block text-sm font-medium text-slate-700">{{ 'account.phoneOptional' | t }}</label>
              <input id="phone" type="tel" formControlName="phone" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
            </div>
            <button type="submit" [disabled]="savingProfile()" class="rounded-md bg-brand-600 px-5 py-2 font-medium text-white hover:bg-brand-700 disabled:opacity-50">
              {{ (savingProfile() ? 'account.saving' : 'account.saveProfile') | t }}
            </button>
          </form>
        </section>

        <section class="rounded-xl border border-slate-200 bg-white p-5">
          <h2 class="mb-3 font-semibold text-slate-900">{{ 'account.changePassword' | t }}</h2>
          <form [formGroup]="passwordForm" (ngSubmit)="changePassword()" class="space-y-3">
            <div>
              <label for="current" class="mb-1 block text-sm font-medium text-slate-700">{{ 'account.currentPassword' | t }}</label>
              <input id="current" type="password" autocomplete="current-password" formControlName="currentPassword" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
            </div>
            <div>
              <label for="next" class="mb-1 block text-sm font-medium text-slate-700">{{ 'account.newPassword' | t }}</label>
              <input id="next" type="password" autocomplete="new-password" formControlName="newPassword" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
              <p class="mt-1 text-xs text-slate-400">{{ 'account.passwordPolicy' | t }}</p>
            </div>
            <button type="submit" [disabled]="changingPassword()" class="rounded-md bg-slate-800 px-5 py-2 font-medium text-white hover:bg-slate-900 disabled:opacity-50">
              {{ (changingPassword() ? 'account.changing' : 'account.changePassword') | t }}
            </button>
          </form>
        </section>
      </div>
    }
  `,
})
export class ProfileComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly toasts = inject(ToastService);
  private readonly i18n = inject(TranslationService);

  protected readonly profile = signal<UserProfile | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly savingProfile = signal(false);
  protected readonly changingPassword = signal(false);

  protected readonly profileForm = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    phone: ['', [Validators.maxLength(30)]],
  });

  protected readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.pattern(STRONG_PASSWORD)]],
  });

  ngOnInit(): void {
    this.reload();
  }

  protected reload(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.auth.getProfile().subscribe({
      next: (p) => {
        this.profile.set(p);
        this.profileForm.reset({ fullName: p.fullName, phone: p.phone ?? '' });
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  protected saveProfile(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }
    const v = this.profileForm.getRawValue();
    this.savingProfile.set(true);
    this.auth.updateProfile(v.fullName.trim(), v.phone.trim() || null).subscribe({
      next: (p) => {
        this.savingProfile.set(false);
        this.profile.set(p);
        this.toasts.success(this.i18n.t('account.profileUpdated'));
      },
      error: () => this.savingProfile.set(false),
    });
  }

  protected changePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      this.toasts.info(this.i18n.t('account.passwordMustMeet'));
      return;
    }
    const v = this.passwordForm.getRawValue();
    this.changingPassword.set(true);
    this.auth.changePassword(v.currentPassword, v.newPassword).subscribe({
      next: () => {
        this.changingPassword.set(false);
        this.passwordForm.reset({ currentPassword: '', newPassword: '' });
        this.toasts.success(this.i18n.t('account.passwordChanged'));
      },
      error: () => this.changingPassword.set(false),
    });
  }
}
