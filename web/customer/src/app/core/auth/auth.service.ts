import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, shareReplay, tap, catchError, map, finalize } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResult, TokenResponse } from '../models';

/**
 * Holds the access token in memory ONLY (never localStorage/sessionStorage), so an XSS payload
 * has no persisted token to steal. The long-lived refresh token lives in an HttpOnly cookie the
 * JS never sees; `refresh()` exchanges it for a fresh access token. On a full page reload the
 * in-memory token is gone, so the app calls `restoreSession()` once at startup to silently
 * re-acquire one from the cookie.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = `${environment.apiBaseUrl}/auth`;

  private readonly accessToken = signal<string | null>(null);
  readonly email = signal<string | null>(null);
  readonly isAuthenticated = computed(() => this.accessToken() !== null);

  /** In-flight refresh shared across concurrent 401s so only one refresh request is sent. */
  private refresh$: Observable<string | null> | null = null;

  private readonly http = inject(HttpClient);

  token(): string | null {
    return this.accessToken();
  }

  register(email: string, password: string, fullName: string): Observable<void> {
    return this.http
      .post<AuthResult>(`${this.api}/register`, { email, password, fullName })
      .pipe(map((r) => this.acceptAuth(r)));
  }

  login(email: string, password: string): Observable<void> {
    return this.http
      .post<AuthResult>(`${this.api}/login`, { email, password })
      .pipe(map((r) => this.acceptAuth(r)));
  }

  /**
   * Exchange the refresh cookie for a new access token. Single-flight: simultaneous callers
   * (e.g. several requests that all 401 at once) share one HTTP call.
   */
  refresh(): Observable<string | null> {
    if (this.refresh$) return this.refresh$;

    this.refresh$ = this.http
      .post<TokenResponse>(`${this.api}/refresh`, {})
      .pipe(
        map((r) => {
          this.accessToken.set(r.accessToken);
          this.email.set(this.emailFromJwt(r.accessToken) ?? this.email());
          return r.accessToken;
        }),
        catchError(() => {
          this.clear();
          return of(null);
        }),
        finalize(() => (this.refresh$ = null)),
        shareReplay(1),
      );
    return this.refresh$;
  }

  /** Called once at app start to restore a session from the refresh cookie, if any. */
  restoreSession(): Observable<boolean> {
    return this.refresh().pipe(map((t) => t !== null));
  }

  logout(): Observable<void> {
    // Always clear local state, even if the server call fails — the user intends to be logged out.
    return this.http.post<void>(`${this.api}/logout`, {}).pipe(
      catchError(() => of(void 0)),
      tap(() => this.clear()),
      map(() => void 0),
    );
  }

  private acceptAuth(r: AuthResult): void {
    this.accessToken.set(r.accessToken);
    this.email.set(r.email);
  }

  private clear(): void {
    this.accessToken.set(null);
    this.email.set(null);
  }

  /** Read the `email` claim from a JWT for display only (no validation — the server validates). */
  private emailFromJwt(token: string): string | null {
    try {
      const payload = token.split('.')[1];
      const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
      const claims = JSON.parse(json) as Record<string, unknown>;
      const email = claims['email'];
      return typeof email === 'string' ? email : null;
    } catch {
      return null;
    }
  }
}
