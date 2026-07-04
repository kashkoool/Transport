import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
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
  imports: [TranslatePipe],
  template: `
    <div class="mx-auto max-w-sm text-center">
      @if (failed()) {
        <p class="text-sm text-red-700">{{ 'authCallback.failed' | t }}</p>
      } @else {
        <p class="text-sm text-slate-600">{{ 'authCallback.signingIn' | t }}</p>
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
