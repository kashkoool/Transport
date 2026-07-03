import { ChangeDetectionStrategy, Component, computed, HostListener, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorBus, phosphorSun, phosphorMoon } from '@ng-icons/phosphor-icons/regular';
import { AuthService } from './core/auth/auth.service';
import { ThemeService } from './core/theme/theme.service';
import { ToastComponent } from './core/toast/toast';
import { NotificationBellComponent } from './core/notifications/notification-bell';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastComponent, NotificationBellComponent, NgIcon],
  providers: [provideIcons({ phosphorBus, phosphorSun, phosphorMoon })],
  template: `
    <div class="flex min-h-full flex-col">
      <!-- On the landing the header is dark and merges seamlessly into the hero; elsewhere it's a
           solid white sticky bar. -->
      <header [class]="headerClass()">
        <nav class="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-3">
          <a [routerLink]="auth.homeRoute()" class="flex items-center gap-2.5">
            <span class="grid h-9 w-9 place-items-center rounded-xl bg-linear-to-br from-brand-500 to-brand-700 text-lg shadow-sm"><ng-icon name="phosphorBus" class="text-white" /></span>
            <span class="text-lg font-extrabold tracking-tight">
              TPX<span class="text-brand-600 dark:text-brand-300">Travel</span>
            </span>
          </a>

          <div class="flex items-center gap-1 text-sm sm:gap-2">
            @if (auth.isAdmin()) {
              <a [routerLink]="'/admin/companies'" routerLinkActive="navlink-active" class="navlink">Companies</a>
              <a [routerLink]="'/admin/users'" routerLinkActive="navlink-active" class="navlink">Users</a>
            } @else if (auth.isVendor()) {
              <a [routerLink]="'/vendor/trips'" routerLinkActive="navlink-active" class="navlink">Trips</a>
              <a [routerLink]="'/vendor/buses'" routerLinkActive="navlink-active" class="navlink">Fleet</a>
              <a [routerLink]="'/vendor/reports'" routerLinkActive="navlink-active" class="navlink hidden sm:inline-flex">Reports</a>
            } @else if (auth.isStaff()) {
              <a [routerLink]="'/vendor/desk'" routerLinkActive="navlink-active" class="navlink">Desk</a>
              <a [routerLink]="'/vendor/trips'" routerLinkActive="navlink-active" class="navlink">Trips</a>
            } @else {
              <a [routerLink]="'/'" routerLinkActive="navlink-active" [routerLinkActiveOptions]="{ exact: true }" class="navlink">Search</a>
              @if (auth.isAuthenticated()) {
                <a [routerLink]="'/my-bookings'" routerLinkActive="navlink-active" class="navlink">My trips</a>
              }
            }

            <button
              type="button"
              (click)="theme.toggle()"
              [attr.aria-label]="theme.theme() === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'"
              class="navlink px-2.5"
            >
              <ng-icon [name]="theme.theme() === 'dark' ? 'phosphorSun' : 'phosphorMoon'" class="text-lg" />
            </button>

            @if (auth.isAuthenticated()) {
              <app-notification-bell />
              <a
                [routerLink]="'/account'"
                class="hidden items-center gap-2 rounded-full py-1 pl-1 pr-3 ring-1 bg-slate-50 ring-slate-200 hover:bg-slate-100 dark:bg-white/10 dark:ring-white/15 dark:hover:bg-white/20 sm:flex"
              >
                <span class="grid h-7 w-7 place-items-center rounded-full bg-brand-600 text-xs font-bold text-white">{{ initial() }}</span>
                <span class="max-w-40 truncate text-xs font-medium text-slate-600 dark:text-slate-300">{{ auth.email() }}</span>
              </a>
              <button type="button" (click)="logout()" class="btn btn-ghost px-3 py-1.5">
                Log out
              </button>
            } @else {
              <a [routerLink]="'/login'" class="navlink">Log in</a>
              <a [routerLink]="'/register'" class="btn btn-primary px-4 py-1.5">Sign up</a>
            }
          </div>
        </nav>
      </header>

      <main class="mx-auto w-full max-w-6xl flex-1 px-4 pb-8" [class.pt-8]="!isLanding()">
        <router-outlet />
      </main>

      <footer class="mt-auto bg-ink text-white">
        <div class="mx-auto grid max-w-6xl gap-8 px-4 py-12 sm:grid-cols-2 lg:grid-cols-4">
          <div class="sm:col-span-2 lg:col-span-1">
            <div class="flex items-center gap-2.5">
              <span class="grid h-9 w-9 place-items-center rounded-xl bg-linear-to-br from-brand-500 to-brand-700 text-lg"><ng-icon name="phosphorBus" class="text-white" /></span>
              <span class="text-lg font-extrabold">TPX<span class="text-brand-300">Travel</span></span>
            </div>
            <p class="mt-4 max-w-xs text-sm text-white/60">
              Book bus trips across Syria. Compare every company, pick your seat, and pay securely.
            </p>
            <p class="mt-4 tracking-[0.3em] text-flag">★★★</p>
          </div>

          <div>
            <p class="text-sm font-bold">Explore</p>
            <ul class="mt-3 space-y-2 text-sm text-white/60">
              <li><a [routerLink]="'/'" class="hover:text-white">Search trips</a></li>
              <li><a [routerLink]="'/register'" class="hover:text-white">Create an account</a></li>
              <li><a [routerLink]="'/login'" class="hover:text-white">Log in</a></li>
            </ul>
          </div>

          <div>
            <p class="text-sm font-bold">Why TPX</p>
            <ul class="mt-3 space-y-2 text-sm text-white/60">
              <li>Every major operator</li>
              <li>Live seat maps</li>
              <li>Secure payments</li>
              <li>Instant QR tickets</li>
            </ul>
          </div>

          <div>
            <p class="text-sm font-bold">For operators</p>
            <ul class="mt-3 space-y-2 text-sm text-white/60">
              <li>Sell at the counter</li>
              <li>Manage fleet &amp; trips</li>
              <li>Reports &amp; demand</li>
            </ul>
          </div>
        </div>

        <div class="border-t border-white/10">
          <div class="mx-auto flex max-w-6xl flex-col items-center justify-between gap-2 px-4 py-5 text-xs text-white/50 sm:flex-row">
            <p>© 2026 TPX Travel · Built for Syria</p>
            <p>Made with care 🇸🇾</p>
          </div>
        </div>
      </footer>
    </div>

    <app-toast />
  `,
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);
  private readonly router = inject(Router);

  protected readonly isLanding = signal(this.atLanding());
  /** True once the user scrolls off the top — flips the see-through landing header to frosted glass. */
  protected readonly scrolled = signal(false);

  /**
   * Landing header is a glassmorphism bar: fully see-through over the hero at the top, frosted
   * (translucent + blur) once scrolled. Every other page keeps the solid white sticky header.
   */
  protected readonly headerClass = computed(() => {
    if (!this.isLanding()) {
      return 'sticky top-0 z-40 border-b border-slate-200/70 bg-white/80 text-slate-700 backdrop-blur dark:border-white/10 dark:bg-ink/80 dark:text-slate-200';
    }
    return this.scrolled()
      ? 'fixed inset-x-0 top-0 z-40 border-b border-slate-200/70 bg-white/70 text-slate-900 backdrop-blur-md dark:border-white/10 dark:bg-ink/70 dark:text-white'
      : 'fixed inset-x-0 top-0 z-40 bg-transparent text-white';
  });

  constructor() {
    this.router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.isLanding.set(this.atLanding()));
  }

  @HostListener('window:scroll')
  protected onScroll(): void {
    this.scrolled.set(window.scrollY > 8);
  }

  protected initial(): string {
    return (this.auth.email() ?? '?').charAt(0).toUpperCase();
  }

  protected logout(): void {
    this.auth.logout().subscribe(() => this.router.navigate(['/login']));
  }

  private atLanding(): boolean {
    return (this.router.url.split('?')[0] || '/') === '/';
  }
}
