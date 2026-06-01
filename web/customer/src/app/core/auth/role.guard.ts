import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/**
 * Guards a route to users holding at least one of the given roles. Anonymous users go to login
 * (preserving the target); a signed-in user lacking the role is sent to their own home route
 * rather than shown a forbidden page.
 */
export function roleGuard(...roles: string[]): CanActivateFn {
  return (_route, state) => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!auth.isAuthenticated()) {
      return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
    }
    return auth.hasAny(...roles) ? true : router.parseUrl(auth.homeRoute());
  };
}
