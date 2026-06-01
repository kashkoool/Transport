import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminApiService, CreateCompanyRequest, CreateManagerRequest } from '../../core/api/admin-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { Company, CompanyStatus } from '../../core/models';

const STATUSES: CompanyStatus[] = ['Pending', 'Active', 'Suspended'];

@Component({
  selector: 'app-admin-companies',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  template: `
    <h1 class="mb-6 text-2xl font-bold text-slate-900">Companies</h1>

    <div class="grid gap-6 lg:grid-cols-3">
      <section class="lg:col-span-2">
        <div class="mb-3 flex items-center gap-2 text-sm">
          <span class="text-slate-500">Filter:</span>
          <button type="button" (click)="setFilter(null)" [class.font-semibold]="filter() === null" [class.text-indigo-700]="filter() === null" class="text-slate-500 hover:text-slate-800">All</button>
          @for (s of statuses; track s) {
            <button type="button" (click)="setFilter(s)" [class.font-semibold]="filter() === s" [class.text-indigo-700]="filter() === s" class="text-slate-500 hover:text-slate-800">{{ s }}</button>
          }
        </div>

        @if (loading()) {
          <p class="text-slate-500">Loading…</p>
        } @else if (companies().length === 0) {
          <p class="rounded-lg bg-slate-100 p-4 text-slate-600">No companies.</p>
        } @else {
          <div class="space-y-3">
            @for (c of companies(); track c.id) {
              <div class="rounded-xl border border-slate-200 bg-white p-4">
                <div class="flex items-center justify-between">
                  <div>
                    <p class="font-semibold text-slate-900">{{ c.name }}</p>
                    <p class="text-sm text-slate-500">{{ c.email }}{{ c.phone ? ' · ' + c.phone : '' }}</p>
                  </div>
                  <div class="flex items-center gap-2">
                    <span
                      class="rounded-full px-3 py-1 text-xs font-semibold"
                      [class.bg-emerald-100]="c.status === 'Active'"
                      [class.text-emerald-700]="c.status === 'Active'"
                      [class.bg-amber-100]="c.status === 'Pending'"
                      [class.text-amber-700]="c.status === 'Pending'"
                      [class.bg-rose-100]="c.status === 'Suspended'"
                      [class.text-rose-700]="c.status === 'Suspended'"
                      >{{ c.status }}</span
                    >
                  </div>
                </div>
                <div class="mt-3 flex flex-wrap gap-2 text-sm">
                  @if (c.status !== 'Active') {
                    <button type="button" [disabled]="busyId() === c.id" (click)="activate(c)" class="rounded-md bg-emerald-50 px-3 py-1.5 font-medium text-emerald-700 hover:bg-emerald-100 disabled:opacity-50">Activate</button>
                  }
                  @if (c.status !== 'Suspended') {
                    <button type="button" [disabled]="busyId() === c.id" (click)="suspend(c)" class="rounded-md bg-rose-50 px-3 py-1.5 font-medium text-rose-700 hover:bg-rose-100 disabled:opacity-50">Suspend</button>
                  }
                  <button type="button" (click)="toggleManager(c.id)" class="rounded-md bg-slate-100 px-3 py-1.5 font-medium text-slate-700 hover:bg-slate-200">
                    {{ managerFor() === c.id ? 'Close' : 'Add manager' }}
                  </button>
                </div>

                @if (managerFor() === c.id) {
                  <form [formGroup]="managerForm" (ngSubmit)="createManager(c)" class="mt-3 space-y-2 rounded-lg bg-slate-50 p-3">
                    <input type="text" formControlName="fullName" placeholder="Manager full name" class="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
                    <input type="email" formControlName="email" placeholder="Manager email" class="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
                    <input type="password" formControlName="password" placeholder="Temp password" autocomplete="new-password" class="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
                    <p class="text-xs text-slate-500">Min 10 chars with upper, lower, digit, and a symbol.</p>
                    <button type="submit" [disabled]="busyId() === c.id" class="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50">Create manager</button>
                  </form>
                }
              </div>
            }
          </div>
          <p class="mt-2 text-xs text-slate-400">{{ total() }} company(ies)</p>
        }
      </section>

      <section class="rounded-xl border border-slate-200 bg-white p-4">
        <h2 class="mb-3 font-semibold text-slate-900">Onboard a company</h2>
        <form [formGroup]="form" (ngSubmit)="create()" class="space-y-3">
          <div>
            <label for="name" class="mb-1 block text-sm font-medium text-slate-700">Name</label>
            <input id="name" type="text" formControlName="name" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
          </div>
          <div>
            <label for="cemail" class="mb-1 block text-sm font-medium text-slate-700">Email</label>
            <input id="cemail" type="email" formControlName="email" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
          </div>
          <div>
            <label for="phone" class="mb-1 block text-sm font-medium text-slate-700">Phone (optional)</label>
            <input id="phone" type="text" formControlName="phone" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
          </div>
          <button type="submit" [disabled]="creating()" class="w-full rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-50">
            {{ creating() ? 'Creating…' : 'Create company' }}
          </button>
          <p class="text-xs text-slate-400">New companies start as <b>Pending</b> — activate them to let them sell.</p>
        </form>
      </section>
    </div>
  `,
})
export class AdminCompaniesComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AdminApiService);
  private readonly toasts = inject(ToastService);

  protected readonly statuses = STATUSES;
  protected readonly companies = signal<Company[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly creating = signal(false);
  protected readonly busyId = signal<string | null>(null);
  protected readonly filter = signal<CompanyStatus | null>(null);
  protected readonly managerFor = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    phone: ['', [Validators.maxLength(40)]],
  });

  protected readonly managerForm = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    password: [
      '',
      [Validators.required, Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{10,}$/)],
    ],
  });

  ngOnInit(): void {
    this.load();
  }

  protected setFilter(status: CompanyStatus | null): void {
    this.filter.set(status);
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.listCompanies(1, 50, this.filter() ?? undefined).subscribe({
      next: (page) => {
        this.companies.set(page.items);
        this.total.set(page.total);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected create(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body: CreateCompanyRequest = {
      name: v.name.trim(),
      email: v.email.trim(),
      phone: v.phone.trim() || null,
    };
    this.creating.set(true);
    this.api.createCompany(body).subscribe({
      next: () => {
        this.creating.set(false);
        this.toasts.success(`Company "${body.name}" created (Pending).`);
        this.form.reset({ name: '', email: '', phone: '' });
        this.load();
      },
      error: () => this.creating.set(false),
    });
  }

  protected activate(c: Company): void {
    this.busyId.set(c.id);
    this.api.activateCompany(c.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.toasts.success(`${c.name} activated.`);
        this.load();
      },
      error: () => this.busyId.set(null),
    });
  }

  protected suspend(c: Company): void {
    this.busyId.set(c.id);
    this.api.suspendCompany(c.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.toasts.info(`${c.name} suspended.`);
        this.load();
      },
      error: () => this.busyId.set(null),
    });
  }

  protected toggleManager(companyId: string): void {
    this.managerFor.set(this.managerFor() === companyId ? null : companyId);
    this.managerForm.reset({ fullName: '', email: '', password: '' });
  }

  protected createManager(c: Company): void {
    if (this.managerForm.invalid) {
      this.managerForm.markAllAsTouched();
      return;
    }
    const body: CreateManagerRequest = this.managerForm.getRawValue();
    this.busyId.set(c.id);
    this.api.createManager(c.id, body).subscribe({
      next: (m) => {
        this.busyId.set(null);
        this.managerFor.set(null);
        this.toasts.success(`Manager ${m.email} created for ${c.name}.`);
      },
      error: () => this.busyId.set(null),
    });
  }
}
