import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

/** Sub-navigation for the vendor console. */
@Component({
  selector: 'app-vendor-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="mb-6 flex flex-wrap gap-1 border-b border-slate-200">
      @for (link of links; track link.path) {
        <a
          [routerLink]="link.path"
          routerLinkActive="border-indigo-600 text-indigo-700"
          class="-mb-px border-b-2 border-transparent px-4 py-2 text-sm font-medium text-slate-500 hover:text-slate-800"
          >{{ link.label }}</a
        >
      }
    </nav>
  `,
})
export class VendorNavComponent {
  protected readonly links = [
    { path: '/vendor/trips', label: 'Trips' },
    { path: '/vendor/buses', label: 'Fleet' },
    { path: '/vendor/staff', label: 'Staff' },
    { path: '/vendor/drivers', label: 'Drivers' },
    { path: '/vendor/company', label: 'Company' },
  ];
}
