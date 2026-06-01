import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
import { ToastComponent } from './core/toast/toast';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastComponent],
  template: `
    <div class="flex min-h-full flex-col">
      <header class="border-b border-slate-200 bg-white">
        <nav class="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <a [routerLink]="auth.homeRoute()" class="flex items-center gap-2 text-lg font-bold text-indigo-700">
            <span class="text-2xl">🚌</span> TPX Travel
          </a>
          <div class="flex items-center gap-4 text-sm">
            @if (auth.isAdmin()) {
              <a routerLink="/admin/companies" routerLinkActive="text-indigo-700 font-semibold" class="text-slate-600 hover:text-slate-900">Companies</a>
            } @else if (auth.isVendor()) {
              <a routerLink="/vendor/trips" routerLinkActive="text-indigo-700 font-semibold" class="text-slate-600 hover:text-slate-900">Trips</a>
              <a routerLink="/vendor/buses" routerLinkActive="text-indigo-700 font-semibold" class="text-slate-600 hover:text-slate-900">Fleet</a>
            } @else {
              <a routerLink="/search" routerLinkActive="text-indigo-700 font-semibold" class="text-slate-600 hover:text-slate-900">Search</a>
              @if (auth.isAuthenticated()) {
                <a routerLink="/my-bookings" routerLinkActive="text-indigo-700 font-semibold" class="text-slate-600 hover:text-slate-900">My bookings</a>
              }
            }

            @if (auth.isAuthenticated()) {
              <span class="hidden text-slate-400 sm:inline">{{ auth.email() }}</span>
              <button
                type="button"
                (click)="logout()"
                class="rounded-md bg-slate-100 px-3 py-1.5 font-medium text-slate-700 hover:bg-slate-200"
              >
                Log out
              </button>
            } @else {
              <a routerLink="/login" class="text-slate-600 hover:text-slate-900">Log in</a>
              <a
                routerLink="/register"
                class="rounded-md bg-indigo-600 px-3 py-1.5 font-medium text-white hover:bg-indigo-700"
                >Sign up</a
              >
            }
          </div>
        </nav>
      </header>

      <main class="mx-auto w-full max-w-5xl flex-1 px-4 py-8">
        <router-outlet />
      </main>

      <footer class="border-t border-slate-200 py-4 text-center text-xs text-slate-400">
        TPX Travel — demo customer app
      </footer>
    </div>

    <app-toast />
  `,
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected logout(): void {
    this.auth.logout().subscribe(() => this.router.navigate(['/login']));
  }
}
