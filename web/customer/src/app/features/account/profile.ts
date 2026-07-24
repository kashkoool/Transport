import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorLockKey, phosphorUser, phosphorWarningCircle } from '@ng-icons/phosphor-icons/regular';
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
  imports: [ReactiveFormsModule, NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorLockKey, phosphorUser, phosphorWarningCircle })],
  template: `
    <h1 class="animate-in mb-6 text-2xl font-bold text-slate-900 dark:text-slate-100">{{ 'account.title' | t }}</h1>

    @if (loading()) {
      <div class="grid gap-6 lg:grid-cols-2">
        @for (s of [1, 2]; track s) {
          <div class="card animate-pulse space-y-3 p-5">
            <div class="h-4 w-32 rounded bg-slate-100 dark:bg-white/10"></div>
            <div class="h-11 w-full rounded-2xl bg-slate-100 dark:bg-white/10"></div>
            <div class="h-11 w-full rounded-2xl bg-slate-100 dark:bg-white/10"></div>
          </div>
        }
      </div>
    } @else if (loadFailed()) {
      <div class="card flex flex-col items-center gap-2 p-12 text-center">
        <ng-icon name="phosphorWarningCircle" class="text-4xl text-slate-300 dark:text-slate-600" />
        <p class="mt-2 text-slate-600 dark:text-slate-300">{{ 'account.loadFailed' | t }}</p>
        <button type="button" (click)="reload()" class="btn btn-soft mt-2">{{ 'account.tryAgain' | t }}</button>
      </div>
    } @else if (profile(); as p) {
      <div class="stagger-children grid gap-6 lg:grid-cols-2">
        <section class="card p-5">
          <h2 class="mb-1 flex items-center gap-1.5 font-semibold text-slate-900 dark:text-slate-100">
            <ng-icon name="phosphorUser" class="text-brand-600 dark:text-brand-400" /> {{ 'account.profile' | t }}
          </h2>
          <p class="mb-4 flex items-center gap-2 text-sm text-slate-500 dark:text-slate-400">
            <span class="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-brand-600 text-xs font-bold text-white">{{ p.email.charAt(0).toUpperCase() }}</span>
            {{ 'account.signedInAs' | t }} <span class="font-medium text-slate-700 dark:text-slate-300">{{ p.email }}</span>
          </p>
          <form [formGroup]="profileForm" (ngSubmit)="saveProfile()" class="space-y-4">
            <div>
              <label for="fullName" class="label">{{ 'account.fullName' | t }}</label>
              <input id="fullName" type="text" formControlName="fullName" class="input" />
            </div>
            <div>
              <label for="phone" class="label">{{ 'account.phoneOptional' | t }}</label>
              <input id="phone" type="tel" formControlName="phone" class="input" />
            </div>
            <button type="submit" [disabled]="savingProfile()" class="btn btn-primary w-full">
              {{ (savingProfile() ? 'account.saving' : 'account.saveProfile') | t }}
            </button>
          </form>
        </section>

        <section class="card p-5">
          <h2 class="mb-4 flex items-center gap-1.5 font-semibold text-slate-900 dark:text-slate-100">
            <ng-icon name="phosphorLockKey" class="text-brand-600 dark:text-brand-400" /> {{ 'account.changePassword' | t }}
          </h2>
          <form [formGroup]="passwordForm" (ngSubmit)="changePassword()" class="space-y-4">
            <div>
              <label for="current" class="label">{{ 'account.currentPassword' | t }}</label>
              <input id="current" type="password" autocomplete="current-password" formControlName="currentPassword" class="input" />
            </div>
            <div>
              <label for="next" class="label">{{ 'account.newPassword' | t }}</label>
              <input id="next" type="password" autocomplete="new-password" formControlName="newPassword" class="input" />
              <p class="mt-1.5 text-xs text-slate-500 dark:text-slate-400">{{ 'account.passwordPolicy' | t }}</p>
            </div>
            <button type="submit" [disabled]="changingPassword()" class="btn btn-dark w-full">
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
