import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, shareReplay, tap, catchError, map, finalize } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResult, TokenResponse, UserProfile } from '../models';

// ASP.NET emits role claims under this URI when MapInboundClaims is off.
const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

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
  readonly roles = signal<string[]>([]);
  readonly isAuthenticated = computed(() => this.accessToken() !== null);
  readonly isAdmin = computed(() => this.hasAny('Admin', 'SuperAdmin'));
  readonly isVendor = computed(() => this.hasAny('VendorManager'));
  readonly isStaff = computed(() => this.hasAny('Staff'));
  readonly isCustomer = computed(() => this.hasAny('Customer'));

  /** In-flight refresh shared across concurrent 401s so only one refresh request is sent. */
  private refresh$: Observable<string | null> | null = null;

  private readonly http = inject(HttpClient);

  token(): string | null {
    return this.accessToken();
  }

  hasAny(...roles: string[]): boolean {
    const mine = this.roles();
    return roles.some((r) => mine.includes(r));
  }

  /** Landing route for the signed-in user's primary role. */
  homeRoute(): string {
    if (this.isAdmin()) return '/admin/companies';
    if (this.isVendor()) return '/vendor/trips';
    if (this.isStaff()) return '/vendor/desk';
    return '/';
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
          this.email.set(this.claim(r.accessToken, 'email') ?? this.email());
          this.roles.set(this.rolesFromJwt(r.accessToken));
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

  /** Request a password-reset email. Always succeeds (anti-enumeration) — never reveals the account. */
  forgotPassword(email: string): Observable<void> {
    return this.http.post<unknown>(`${this.api}/forgot-password`, { email }).pipe(map(() => void 0));
  }

  /** Complete a password reset with the emailed token. Errors (invalid/expired) surface via the interceptor. */
  resetPassword(email: string, token: string, newPassword: string): Observable<void> {
    return this.http
      .post<unknown>(`${this.api}/reset-password`, { email, token, newPassword })
      .pipe(map(() => void 0));
  }

  /** Confirm an email address with the emailed token. */
  verifyEmail(email: string, token: string): Observable<void> {
    return this.http.post<unknown>(`${this.api}/verify-email`, { email, token }).pipe(map(() => void 0));
  }

  /** Re-send the verification email if applicable. Always succeeds (anti-enumeration). */
  resendVerification(email: string): Observable<void> {
    return this.http.post<unknown>(`${this.api}/resend-verification`, { email }).pipe(map(() => void 0));
  }

  /** The authenticated caller's profile (name, phone, email, roles). */
  getProfile(): Observable<UserProfile> {
    return this.http.get<UserProfile>(`${this.api}/profile`);
  }

  /** Update the caller's own name + phone (email is read-only). */
  updateProfile(fullName: string, phone: string | null): Observable<UserProfile> {
    return this.http.put<UserProfile>(`${this.api}/profile`, { fullName, phone });
  }

  /** Change the caller's password (verifies the current one; the server revokes other sessions). */
  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http
      .post<unknown>(`${this.api}/change-password`, { currentPassword, newPassword })
      .pipe(map(() => void 0));
  }

  /**
   * Start the Google sign-in/sign-up flow with a full-page navigation to the backend, which
   * redirects to Google and, on success, sets the refresh cookie and bounces back to /auth/callback.
   * `returnUrl` is where the SPA should land afterwards (must be an app-relative path).
   */
  loginWithGoogle(returnUrl?: string): void {
    const query = returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : '';
    window.location.href = `${this.api}/google/start${query}`;
  }

  private acceptAuth(r: AuthResult): void {
    this.accessToken.set(r.accessToken);
    this.email.set(r.email);
    this.roles.set(r.roles ?? []);
  }

  private clear(): void {
    this.accessToken.set(null);
    this.email.set(null);
    this.roles.set([]);
  }

  /** Read a single string claim from a JWT for display only (the server validates the token). */
  private claim(token: string, name: string): string | null {
    const claims = this.decode(token);
    const value = claims?.[name];
    return typeof value === 'string' ? value : null;
  }

  /** Normalize the role claim (single string or array) to a string[]. */
  private rolesFromJwt(token: string): string[] {
    const claims = this.decode(token);
    const value = claims?.[ROLE_CLAIM] ?? claims?.['role'] ?? claims?.['roles'];
    if (Array.isArray(value)) return value.filter((v): v is string => typeof v === 'string');
    return typeof value === 'string' ? [value] : [];
  }

  private decode(token: string): Record<string, unknown> | null {
    try {
      const payload = token.split('.')[1];
      const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
      return JSON.parse(json) as Record<string, unknown>;
    } catch {
      return null;
    }
  }
}
