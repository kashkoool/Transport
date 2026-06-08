import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ToastService } from '../toast/toast.service';
import { ProblemDetails } from '../models';

/**
 * Turns failed API calls into a single user-facing toast. Uses the backend's stable
 * ProblemDetails `detail`/`title` so the message is meaningful without leaking internals.
 * 401s are skipped — the auth interceptor (which runs closer to the backend) silently refreshes
 * and retries those, and only a truly-expired session should surface, as a redirect to login.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toasts = inject(ToastService);
  // The startup session-restore (/auth/refresh) returns 400 by design for an anonymous visitor;
  // AuthService already handles it, so it must never flash a toast on a cold load.
  const isSilentSessionCall = req.url.endsWith('/auth/refresh');

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status !== 401 && !isSilentSessionCall) {
        toasts.error(messageFor(err));
      }
      return throwError(() => err);
    }),
  );
};

function messageFor(err: HttpErrorResponse): string {
  // Network / CORS / server-unreachable: err.status === 0.
  if (err.status === 0) {
    return 'Cannot reach the server. Check your connection and try again.';
  }
  const problem = err.error as ProblemDetails | string | null;
  if (problem && typeof problem === 'object') {
    return problem.detail || problem.title || 'Something went wrong. Please try again.';
  }
  if (typeof problem === 'string' && problem.trim().length > 0) {
    return problem;
  }
  return 'Something went wrong. Please try again.';
}
