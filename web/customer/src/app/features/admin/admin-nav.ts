import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorBuildings, phosphorUsers, phosphorChartBar } from '@ng-icons/phosphor-icons/regular';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/**
 * Sub-navigation for the admin console — a responsive sidebar shared by every /admin/* page.
 * Vertical, sticky column on desktop; a horizontally-scrolling pill bar on mobile.
 */
@Component({
  selector: 'app-admin-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorBuildings, phosphorUsers, phosphorChartBar })],
  template: `
    <nav
      class="-mx-1 mb-6 flex gap-1.5 overflow-x-auto px-1 pb-1 lg:sticky lg:top-6 lg:mx-0 lg:mb-0 lg:w-56 lg:shrink-0 lg:flex-col lg:overflow-visible lg:px-0 lg:pb-0"
      aria-label="Admin sections"
    >
      @for (link of links; track link.path) {
        <a
          [routerLink]="link.path"
          routerLinkActive="bg-brand-50 text-brand-700 dark:bg-brand-500/15 dark:text-brand-300"
          class="flex shrink-0 items-center gap-3 rounded-2xl px-4 py-2.5 text-sm font-semibold text-slate-500 transition-colors duration-150 hover:bg-slate-100 hover:text-slate-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 focus-visible:ring-offset-2 dark:text-slate-400 dark:hover:bg-white/5 dark:hover:text-slate-100"
        >
          <ng-icon [name]="link.icon" class="shrink-0 text-base" />
          <span>{{ link.label | t }}</span>
        </a>
      }
    </nav>
  `,
})
export class AdminNavComponent {
  protected readonly links = [
    { path: '/admin/companies', label: 'admin.nav.companies', icon: 'phosphorBuildings' },
    { path: '/admin/users', label: 'admin.nav.customers', icon: 'phosphorUsers' },
    { path: '/admin/reports', label: 'admin.nav.reports', icon: 'phosphorChartBar' },
  ];
}
