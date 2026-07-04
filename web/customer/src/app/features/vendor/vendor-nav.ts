import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/** Sub-navigation for the vendor console. Staff (non-manager) see only the Desk. */
@Component({
  selector: 'app-vendor-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, TranslatePipe],
  template: `
    <nav class="mb-6 flex flex-wrap gap-1 border-b border-slate-200">
      @for (link of links(); track link.path) {
        <a
          [routerLink]="link.path"
          routerLinkActive="border-brand-600 text-brand-700"
          class="-mb-px border-b-2 border-transparent px-4 py-2 text-sm font-medium text-slate-500 hover:text-slate-800"
          >{{ link.label | t }}</a
        >
      }
    </nav>
  `,
})
export class VendorNavComponent {
  private readonly auth = inject(AuthService);

  private readonly managerLinks = [
    { path: '/vendor/trips', label: 'vendor.nav.trips' },
    { path: '/vendor/buses', label: 'vendor.nav.fleet' },
    { path: '/vendor/staff', label: 'vendor.nav.staff' },
    { path: '/vendor/drivers', label: 'vendor.nav.drivers' },
    { path: '/vendor/desk', label: 'vendor.nav.desk' },
    { path: '/vendor/promo', label: 'vendor.nav.promo' },
    { path: '/vendor/reports', label: 'vendor.nav.reports' },
    { path: '/vendor/company', label: 'vendor.nav.company' },
  ];

  // Staff share trip management with managers (docs: Manager + Employee) plus the Desk.
  private readonly staffLinks = [
    { path: '/vendor/trips', label: 'vendor.nav.trips' },
    { path: '/vendor/desk', label: 'vendor.nav.desk' },
  ];

  protected readonly links = computed(() => (this.auth.isVendor() ? this.managerLinks : this.staffLinks));
}
