import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Attaches the in-memory access token and `withCredentials` (so the HttpOnly refresh cookie
 * flows) to every API call. On a 401 from a protected endpoint it performs ONE silent refresh
 * and retries the original request; if the refresh fails the user is sent to the login page.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  // Auth endpoints (login/register/refresh/logout) rely on the cookie, not the bearer, and must
  // not trigger the refresh-retry loop.
  const isAuthEndpoint = req.url.includes('/auth/');

  let request = req.clone({ withCredentials: true });
  const token = auth.token();
  if (token && !isAuthEndpoint) {
    request = request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }

  return next(request).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status !== 401 || isAuthEndpoint) {
        return throwError(() => err);
      }
      return auth.refresh().pipe(
        switchMap((newToken) => {
          if (!newToken) {
            router.navigate(['/login']);
            return throwError(() => err);
          }
          const retried = req.clone({
            withCredentials: true,
            setHeaders: { Authorization: `Bearer ${newToken}` },
          });
          return next(retried);
        }),
      );
    }),
  );
};
