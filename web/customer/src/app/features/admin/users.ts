import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { AdminApiService } from '../../core/api/admin-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { CustomerAccount } from '../../core/models';
import { AdminNavComponent } from './admin-nav';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-admin-users',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AdminNavComponent, TranslatePipe],
  template: `
    <app-admin-nav />
    <h1 class="mb-6 text-2xl font-bold text-slate-900">{{ 'admin.customers.title' | t }}</h1>

    <input
      type="search"
      [value]="search()"
      (input)="onSearch($event)"
      [placeholder]="'admin.customers.searchPlaceholder' | t"
      class="mb-3 w-full max-w-sm rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
    />

    @if (loading()) {
      <p class="text-slate-500">{{ 'admin.customers.loading' | t }}</p>
    } @else if (customers().length === 0) {
      <p class="rounded-lg bg-slate-100 p-4 text-slate-600">{{ 'admin.customers.empty' | t }}</p>
    } @else {
      <div class="overflow-x-auto rounded-xl border border-slate-200 bg-white">
        <table class="w-full text-sm">
          <thead class="bg-slate-50 text-left text-slate-500">
            <tr>
              <th class="px-4 py-2 font-medium">{{ 'admin.customers.colName' | t }}</th>
              <th class="px-4 py-2 font-medium">{{ 'admin.customers.colEmail' | t }}</th>
              <th class="px-4 py-2 font-medium">{{ 'admin.customers.colStatus' | t }}</th>
              <th class="px-4 py-2 font-medium"></th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100">
            @for (c of customers(); track c.id) {
              <tr>
                <td class="px-4 py-2 font-medium text-slate-800">{{ c.fullName || '—' }}</td>
                <td class="px-4 py-2 text-slate-500">{{ c.email }}</td>
                <td class="px-4 py-2">
                  <span class="rounded-full px-2 py-0.5 text-xs font-semibold"
                    [class.bg-emerald-100]="!c.suspended" [class.text-emerald-700]="!c.suspended"
                    [class.bg-rose-100]="c.suspended" [class.text-rose-700]="c.suspended">
                    {{ (c.suspended ? 'admin.status.suspended' : 'admin.status.active') | t }}
                  </span>
                </td>
                <td class="px-4 py-2 text-right whitespace-nowrap">
                  @if (c.suspended) {
                    <button type="button" [disabled]="busy() === c.id" (click)="reactivate(c)" class="text-sm font-medium text-emerald-600 hover:text-emerald-700">{{ 'admin.customers.reactivate' | t }}</button>
                  } @else {
                    <button type="button" [disabled]="busy() === c.id" (click)="suspend(c)" class="text-sm font-medium text-amber-600 hover:text-amber-700">{{ 'admin.customers.suspend' | t }}</button>
                  }
                  @if (confirmingDelete() === c.id) {
                    <button type="button" [disabled]="busy() === c.id" (click)="remove(c)" class="ml-3 text-sm font-medium text-rose-700">{{ 'admin.customers.confirm' | t }}</button>
                    <button type="button" (click)="confirmingDelete.set(null)" class="ml-2 text-sm text-slate-500">{{ 'admin.customers.keep' | t }}</button>
                  } @else {
                    <button type="button" (click)="confirmingDelete.set(c.id)" class="ml-3 text-sm font-medium text-rose-600 hover:text-rose-700">{{ 'admin.customers.delete' | t }}</button>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <p class="mt-2 text-xs text-slate-400">{{ 'admin.customers.showing' | t: { shown: customers().length, total: total() } }}</p>
    }
  `,
})
export class AdminUsersComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly toasts = inject(ToastService);
  private readonly i18n = inject(TranslationService);

  protected readonly customers = signal<CustomerAccount[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly busy = signal<string | null>(null);
  protected readonly confirmingDelete = signal<string | null>(null);
  protected readonly search = signal('');
  private searchTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.load();
  }

  protected onSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.load(), 250); // debounce
  }

  private load(): void {
    this.loading.set(true);
    this.api.listCustomers(1, 50, this.search().trim() || undefined).subscribe({
      next: (page) => {
        this.customers.set(page.data);
        this.total.set(page.total);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected suspend(c: CustomerAccount): void {
    this.busy.set(c.id);
    this.api.suspendCustomer(c.id).subscribe({
      next: () => { this.busy.set(null); this.toasts.info(this.i18n.t('admin.customers.toast.suspended', { email: c.email })); this.load(); },
      error: () => this.busy.set(null),
    });
  }

  protected reactivate(c: CustomerAccount): void {
    this.busy.set(c.id);
    this.api.reactivateCustomer(c.id).subscribe({
      next: () => { this.busy.set(null); this.toasts.success(this.i18n.t('admin.customers.toast.reactivated', { email: c.email })); this.load(); },
      error: () => this.busy.set(null),
    });
  }

  protected remove(c: CustomerAccount): void {
    this.busy.set(c.id);
    this.api.deleteCustomer(c.id).subscribe({
      next: () => { this.busy.set(null); this.confirmingDelete.set(null); this.toasts.success(this.i18n.t('admin.customers.toast.deleted', { email: c.email })); this.load(); },
      error: () => { this.busy.set(null); this.confirmingDelete.set(null); },
    });
  }
}
