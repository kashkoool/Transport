import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  phosphorPath,
  phosphorBus,
  phosphorUsers,
  phosphorSteeringWheel,
  phosphorTicket,
  phosphorTag,
  phosphorChartBar,
  phosphorBuildings,
} from '@ng-icons/phosphor-icons/regular';
import { AuthService } from '../../core/auth/auth.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/** Sub-navigation for the vendor console. Staff (non-manager) see only Trips + the Desk. */
@Component({
  selector: 'app-vendor-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, NgIcon, TranslatePipe],
  providers: [
    provideIcons({
      phosphorPath,
      phosphorBus,
      phosphorUsers,
      phosphorSteeringWheel,
      phosphorTicket,
      phosphorTag,
      phosphorChartBar,
      phosphorBuildings,
    }),
  ],
  host: { class: 'block' },
  template: `
    <nav
      aria-label="Vendor console"
      class="-mx-1 mb-6 flex gap-1 overflow-x-auto px-1 pb-1
        lg:sticky lg:top-20 lg:mb-0 lg:w-60 lg:shrink-0 lg:flex-col lg:gap-0.5 lg:overflow-visible lg:rounded-2xl
        lg:border lg:border-slate-200/80 lg:bg-white lg:p-2.5 lg:px-2 lg:shadow-sm
        dark:lg:border-white/10 dark:lg:bg-ink-800"
    >
      @for (link of links(); track link.path; let i = $index) {
        @if (i === 4 && links().length > 4) {
          <div class="hidden lg:my-2 lg:block lg:border-t lg:border-slate-100 dark:lg:border-white/10" aria-hidden="true"></div>
        }
        <a
          [routerLink]="link.path"
          routerLinkActive="bg-brand-50 text-brand-700 font-semibold dark:bg-brand-500/15 dark:text-brand-300 lg:border-brand-600 dark:lg:border-brand-400"
          class="flex shrink-0 items-center gap-2 rounded-full border-transparent px-3.5 py-2 text-sm font-medium
            text-slate-600 transition-colors duration-150 hover:bg-slate-100 hover:text-slate-900
            dark:text-slate-300 dark:hover:bg-white/5 dark:hover:text-white
            lg:w-full lg:gap-2.5 lg:rounded-xl lg:border-s-[3px] lg:px-3 lg:py-2.5"
        >
          <ng-icon [name]="link.icon" class="shrink-0 text-base lg:text-lg" aria-hidden="true" />
          <span class="truncate">{{ link.label | t }}</span>
        </a>
      }
    </nav>
  `,
})
export class VendorNavComponent {
  private readonly auth = inject(AuthService);

  private readonly managerLinks = [
    { path: '/vendor/trips', label: 'vendor.nav.trips', icon: 'phosphorPath' },
    { path: '/vendor/buses', label: 'vendor.nav.fleet', icon: 'phosphorBus' },
    { path: '/vendor/staff', label: 'vendor.nav.staff', icon: 'phosphorUsers' },
    { path: '/vendor/drivers', label: 'vendor.nav.drivers', icon: 'phosphorSteeringWheel' },
    { path: '/vendor/desk', label: 'vendor.nav.desk', icon: 'phosphorTicket' },
    { path: '/vendor/promo', label: 'vendor.nav.promo', icon: 'phosphorTag' },
    { path: '/vendor/reports', label: 'vendor.nav.reports', icon: 'phosphorChartBar' },
    { path: '/vendor/company', label: 'vendor.nav.company', icon: 'phosphorBuildings' },
  ];

  // Staff share trip management with managers (docs: Manager + Employee) plus the Desk.
  private readonly staffLinks = [
    { path: '/vendor/trips', label: 'vendor.nav.trips', icon: 'phosphorPath' },
    { path: '/vendor/desk', label: 'vendor.nav.desk', icon: 'phosphorTicket' },
  ];

  protected readonly links = computed(() => (this.auth.isVendor() ? this.managerLinks : this.staffLinks));
}
