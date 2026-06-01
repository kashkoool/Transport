import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

/** Sub-navigation for the vendor console (Trips / Fleet). */
@Component({
  selector: 'app-vendor-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="mb-6 flex gap-1 border-b border-slate-200">
      <a
        routerLink="/vendor/trips"
        routerLinkActive="border-indigo-600 text-indigo-700"
        class="-mb-px border-b-2 border-transparent px-4 py-2 text-sm font-medium text-slate-500 hover:text-slate-800"
        >Trips</a
      >
      <a
        routerLink="/vendor/buses"
        routerLinkActive="border-indigo-600 text-indigo-700"
        class="-mb-px border-b-2 border-transparent px-4 py-2 text-sm font-medium text-slate-500 hover:text-slate-800"
        >Fleet</a
      >
    </nav>
  `,
})
export class VendorNavComponent {}
