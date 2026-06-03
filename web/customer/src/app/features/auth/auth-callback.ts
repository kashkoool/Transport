import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

/**
 * Landing page after an external (Google) sign-in. The backend has already set the HttpOnly
 * refresh cookie, so we restore the in-memory session from it and route the user onward.
 */
@Component({
  selector: 'app-auth-callback',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-sm text-center">
      @if (failed()) {
        <p class="text-sm text-red-700">Sign-in failed. Redirecting…</p>
      } @else {
        <p class="text-sm text-slate-600">Signing you in…</p>
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
      const safe = !!returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//');
      this.router.navigateByUrl(safe ? returnUrl! : this.auth.homeRoute());
    });
  }
}
