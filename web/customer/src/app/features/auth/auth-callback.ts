import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorCircleNotch, phosphorWarningCircle } from '@ng-icons/phosphor-icons/regular';
import { AuthService } from '../../core/auth/auth.service';
import { safeReturnPath } from '../../core/auth/safe-return';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/**
 * Landing page after an external (Google) sign-in. The backend has already set the HttpOnly
 * refresh cookie, so we restore the in-memory session from it and route the user onward.
 */
@Component({
  selector: 'app-auth-callback',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorCircleNotch, phosphorWarningCircle })],
  template: `
    <div class="mx-auto flex min-h-[60vh] max-w-sm items-center justify-center text-center">
      @if (failed()) {
        <p class="flex items-center gap-2 text-sm text-rose-700 dark:text-rose-400">
          <ng-icon name="phosphorWarningCircle" /> {{ 'authCallback.failed' | t }}
        </p>
      } @else {
        <p class="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400">
          <ng-icon name="phosphorCircleNotch" class="animate-spin text-brand-500" /> {{ 'authCallback.signingIn' | t }}
        </p>
      }
    </div>
  `,
})
export class AuthCallbackComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly failed = signal(false);

  constructor() {
    this.auth.restoreSession().subscribe((ok) => {
      if (!ok) {
        this.failed.set(true);
        this.router.navigate(['/login'], { queryParams: { error: 'google' } });
        return;
      }
      // Only app-relative paths (mirror the backend SafeReturnPath: reject protocol-relative "//").
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
      this.router.navigateByUrl(safeReturnPath(returnUrl, this.auth.homeRoute()));
    });
  }
}
