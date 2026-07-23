import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorMagnifyingGlass, phosphorTrash, phosphorUsers } from '@ng-icons/phosphor-icons/regular';
import { AdminApiService } from '../../core/api/admin-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { CustomerAccount } from '../../core/models';
import { AdminNavComponent } from './admin-nav';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-admin-users',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AdminNavComponent, NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorMagnifyingGlass, phosphorTrash, phosphorUsers })],
  template: `
    <div class="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <div class="lg:flex lg:items-start lg:gap-8">
        <app-admin-nav />

        <div class="min-w-0 flex-1">
          <h1 class="animate-in mb-6 text-2xl font-bold text-slate-900 dark:text-white">{{ 'admin.customers.title' | t }}</h1>

          <!-- Toolbar -->
          <div class="relative mb-4 max-w-sm">
            <ng-icon name="phosphorMagnifyingGlass" class="pointer-events-none absolute inset-y-0 start-4 my-auto text-base text-slate-400" />
            <input
              type="search"
              [value]="search()"
              (input)="onSearch($event)"
              [placeholder]="'admin.customers.searchPlaceholder' | t"
              class="input ps-10"
            />
          </div>

          @if (loading()) {
            <div class="card overflow-hidden" aria-hidden="true">
              <div class="border-b border-slate-100 bg-slate-50 px-4 py-3 dark:border-white/10 dark:bg-white/3 sm:px-6">
                <div class="h-3 w-40 animate-pulse rounded-full bg-slate-200 dark:bg-white/10"></div>
              </div>
              <div class="divide-y divide-slate-100 dark:divide-white/5">
                @for (i of [0, 1, 2, 3, 4]; track i) {
                  <div class="flex items-center gap-4 px-4 py-4 sm:px-6">
                    <div class="h-4 w-32 animate-pulse rounded bg-slate-200 dark:bg-white/10"></div>
                    <div class="h-4 w-40 animate-pulse rounded bg-slate-100 dark:bg-white/5"></div>
                    <div class="ms-auto h-6 w-16 animate-pulse rounded-full bg-slate-100 dark:bg-white/5"></div>
                  </div>
                }
              </div>
            </div>
          } @else if (customers().length === 0) {
            <div class="card flex flex-col items-center gap-3 border-dashed px-6 py-14 text-center">
              <span class="grid h-12 w-12 place-items-center rounded-full bg-slate-100 text-slate-400 dark:bg-white/5 dark:text-slate-500">
                <ng-icon name="phosphorUsers" class="text-2xl" />
              </span>
              <p class="text-sm font-medium text-slate-500 dark:text-slate-400">{{ 'admin.customers.empty' | t }}</p>
            </div>
          } @else {
            <div class="card overflow-x-auto">
              <table class="w-full text-start text-sm">
                <thead class="border-b border-slate-100 text-xs font-semibold uppercase tracking-wide text-slate-400 dark:border-white/10 dark:text-slate-500">
                  <tr>
                    <th class="px-4 py-3 text-start font-semibold sm:px-6">{{ 'admin.customers.colName' | t }}</th>
                    <th class="px-4 py-3 text-start font-semibold sm:px-6">{{ 'admin.customers.colEmail' | t }}</th>
                    <th class="px-4 py-3 text-start font-semibold sm:px-6">{{ 'admin.customers.colStatus' | t }}</th>
                    <th class="px-4 py-3 text-end font-semibold sm:px-6"></th>
                  </tr>
                </thead>
                <tbody class="stagger-children divide-y divide-slate-100 dark:divide-white/5">
                  @for (c of customers(); track c.id) {
                    <tr class="transition-colors hover:bg-slate-50 dark:hover:bg-white/3">
                      <td class="px-4 py-3 font-semibold text-slate-800 dark:text-slate-100 sm:px-6">{{ c.fullName || '—' }}</td>
                      <td class="px-4 py-3 text-slate-500 dark:text-slate-400 sm:px-6">{{ c.email }}</td>
                      <td class="px-4 py-3 sm:px-6">
                        <span class="badge" [class]="c.suspended ? 'bg-rose-50 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300' : 'badge-brand'">
                          {{ (c.suspended ? 'admin.status.suspended' : 'admin.status.active') | t }}
                        </span>
                      </td>
                      <td class="px-4 py-3 text-end whitespace-nowrap sm:px-6">
                        <div class="inline-flex items-center gap-3">
                          @if (c.suspended) {
                            <button type="button" [disabled]="busy() === c.id" (click)="reactivate(c)" class="text-sm font-semibold text-brand-600 hover:text-brand-700 disabled:opacity-50 dark:text-brand-400 dark:hover:text-brand-300">{{ 'admin.customers.reactivate' | t }}</button>
                          } @else {
                            <button type="button" [disabled]="busy() === c.id" (click)="suspend(c)" class="text-sm font-semibold text-amber-600 hover:text-amber-700 disabled:opacity-50 dark:text-amber-400 dark:hover:text-amber-300">{{ 'admin.customers.suspend' | t }}</button>
                          }
                          <span class="h-4 w-px bg-slate-200 dark:bg-white/10"></span>
                          @if (confirmingDelete() === c.id) {
                            <button type="button" [disabled]="busy() === c.id" (click)="remove(c)" class="btn btn-danger px-3 py-1.5 text-xs">
                              <ng-icon name="phosphorTrash" class="text-sm" />
                              {{ 'admin.customers.confirm' | t }}
                            </button>
                            <button type="button" (click)="confirmingDelete.set(null)" class="text-sm text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200">{{ 'admin.customers.keep' | t }}</button>
                          } @else {
                            <button type="button" (click)="confirmingDelete.set(c.id)" class="text-sm font-semibold text-rose-600 hover:text-rose-700 dark:text-rose-400 dark:hover:text-rose-300">{{ 'admin.customers.delete' | t }}</button>
                          }
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <p class="mt-3 text-xs text-slate-400 dark:text-slate-500">{{ 'admin.customers.showing' | t: { shown: customers().length, total: total() } }}</p>
          }
        </div>
      </div>
    </div>
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
