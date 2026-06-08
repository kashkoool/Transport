import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
import { ToastComponent } from './core/toast/toast';
import { NotificationBellComponent } from './core/notifications/notification-bell';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastComponent, NotificationBellComponent],
  template: `
    <div class="flex min-h-full flex-col">
      <header class="sticky top-0 z-40 border-b border-slate-200/70 bg-white/80 backdrop-blur">
        <nav class="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-3">
          <a [routerLink]="auth.homeRoute()" class="flex items-center gap-2.5">
            <span
              class="grid h-9 w-9 place-items-center rounded-xl bg-linear-to-br from-brand-500 to-brand-700 text-lg shadow-sm"
              >🚌</span
            >
            <span class="text-lg font-extrabold tracking-tight text-slate-900"
              >TPX<span class="text-brand-600">Travel</span></span
            >
          </a>

          <div class="flex items-center gap-1 text-sm sm:gap-2">
            @if (auth.isAdmin()) {
              <a [routerLink]="'/admin/companies'" routerLinkActive="bg-brand-50 text-brand-700" class="navlink">Companies</a>
              <a [routerLink]="'/admin/users'" routerLinkActive="bg-brand-50 text-brand-700" class="navlink">Users</a>
            } @else if (auth.isVendor()) {
              <a [routerLink]="'/vendor/trips'" routerLinkActive="bg-brand-50 text-brand-700" class="navlink">Trips</a>
              <a [routerLink]="'/vendor/buses'" routerLinkActive="bg-brand-50 text-brand-700" class="navlink">Fleet</a>
              <a [routerLink]="'/vendor/reports'" routerLinkActive="bg-brand-50 text-brand-700" class="navlink hidden sm:inline-flex">Reports</a>
            } @else if (auth.isStaff()) {
              <a [routerLink]="'/vendor/desk'" routerLinkActive="bg-brand-50 text-brand-700" class="navlink">Desk</a>
              <a [routerLink]="'/vendor/trips'" routerLinkActive="bg-brand-50 text-brand-700" class="navlink">Trips</a>
            } @else {
              <a [routerLink]="'/search'" routerLinkActive="bg-brand-50 text-brand-700" class="navlink">Search</a>
              @if (auth.isAuthenticated()) {
                <a [routerLink]="'/my-bookings'" routerLinkActive="bg-brand-50 text-brand-700" class="navlink">My trips</a>
              }
            }

            @if (auth.isAuthenticated()) {
              <app-notification-bell />
              <a
                [routerLink]="'/account'"
                routerLinkActive="ring-brand-200"
                class="hidden items-center gap-2 rounded-full bg-slate-50 py-1 pl-1 pr-3 ring-1 ring-slate-200 hover:bg-slate-100 sm:flex"
              >
                <span class="grid h-7 w-7 place-items-center rounded-full bg-brand-600 text-xs font-bold text-white">{{
                  initial()
                }}</span>
                <span class="max-w-40 truncate text-xs font-medium text-slate-600">{{ auth.email() }}</span>
              </a>
              <button type="button" (click)="logout()" class="btn btn-ghost px-3 py-1.5">Log out</button>
            } @else {
              <a [routerLink]="'/login'" class="navlink">Log in</a>
              <a [routerLink]="'/register'" class="btn btn-primary px-3.5 py-1.5">Sign up</a>
            }
          </div>
        </nav>
      </header>

      <main class="mx-auto w-full max-w-6xl flex-1 px-4 py-8">
        <router-outlet />
      </main>

      <footer class="border-t border-slate-200 bg-white">
        <div class="mx-auto flex max-w-6xl flex-col items-center justify-between gap-3 px-4 py-6 sm:flex-row">
          <div class="flex items-center gap-2 text-sm font-semibold text-slate-700">
            <span class="text-base">🚌</span> TPX Travel
          </div>
          <p class="text-xs text-slate-400">Book bus trips across Syria · © 2026 TPX Travel</p>
        </div>
      </footer>
    </div>

    <app-toast />
  `,
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected initial(): string {
    return (this.auth.email() ?? '?').charAt(0).toUpperCase();
  }

  protected logout(): void {
    this.auth.logout().subscribe(() => this.router.navigate(['/login']));
  }
}
