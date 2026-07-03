import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

/** Sub-navigation for the admin console. */
@Component({
  selector: 'app-admin-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="mb-6 flex flex-wrap gap-1 border-b border-slate-200">
      @for (link of links; track link.path) {
        <a
          [routerLink]="link.path"
          routerLinkActive="border-brand-600 text-brand-700"
          class="-mb-px border-b-2 border-transparent px-4 py-2 text-sm font-medium text-slate-500 hover:text-slate-800"
          >{{ link.label }}</a
        >
      }
    </nav>
  `,
})
export class AdminNavComponent {
  protected readonly links = [
    { path: '/admin/companies', label: 'Companies' },
    { path: '/admin/users', label: 'Customers' },
    { path: '/admin/reports', label: 'Reports' },
  ];
}
