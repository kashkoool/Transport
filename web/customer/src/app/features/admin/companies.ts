import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  phosphorBuildings,
  phosphorCheckCircle,
  phosphorEnvelopeSimple,
  phosphorFunnel,
  phosphorUserPlus,
} from '@ng-icons/phosphor-icons/regular';
import { AdminApiService, CreateCompanyRequest, CreateManagerRequest } from '../../core/api/admin-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { Company, CompanyStatus } from '../../core/models';
import { AdminNavComponent } from './admin-nav';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

const STATUSES: CompanyStatus[] = ['Pending', 'Active', 'Suspended'];

const STATUS_KEY: Record<CompanyStatus, string> = {
  Pending: 'admin.status.pending',
  Active: 'admin.status.active',
  Suspended: 'admin.status.suspended',
};

@Component({
  selector: 'app-admin-companies',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, AdminNavComponent, NgIcon, TranslatePipe],
  providers: [
    provideIcons({ phosphorBuildings, phosphorCheckCircle, phosphorEnvelopeSimple, phosphorFunnel, phosphorUserPlus }),
  ],
  template: `
    <div class="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <div class="lg:flex lg:items-start lg:gap-8">
        <app-admin-nav />

        <div class="min-w-0 flex-1">
          <h1 class="animate-in mb-6 text-2xl font-bold text-slate-900 dark:text-white">{{ 'admin.companies.title' | t }}</h1>

          <div class="grid gap-6 lg:grid-cols-3">
            <section class="lg:col-span-2">
              <!-- Toolbar: status filter -->
              <div class="card mb-4 flex flex-wrap items-center gap-2 p-3">
                <span class="inline-flex items-center gap-1.5 ps-1 text-sm font-medium text-slate-500 dark:text-slate-400">
                  <ng-icon name="phosphorFunnel" class="text-base" />
                  {{ 'admin.companies.filter' | t }}
                </span>
                <div class="flex flex-wrap gap-1.5">
                  <button
                    type="button"
                    (click)="setFilter(null)"
                    class="rounded-full px-3.5 py-1.5 text-sm font-semibold transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 focus-visible:ring-offset-2"
                    [class]="filter() === null ? 'bg-brand-600 text-white shadow-sm' : 'text-slate-500 hover:bg-slate-100 dark:text-slate-400 dark:hover:bg-white/5'"
                  >
                    {{ 'admin.companies.filterAll' | t }}
                  </button>
                  @for (s of statuses; track s) {
                    <button
                      type="button"
                      (click)="setFilter(s)"
                      class="rounded-full px-3.5 py-1.5 text-sm font-semibold transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 focus-visible:ring-offset-2"
                      [class]="filter() === s ? 'bg-brand-600 text-white shadow-sm' : 'text-slate-500 hover:bg-slate-100 dark:text-slate-400 dark:hover:bg-white/5'"
                    >
                      {{ statusKey(s) | t }}
                    </button>
                  }
                </div>
              </div>

              @if (loading()) {
                <div class="space-y-3" aria-hidden="true">
                  @for (i of [0, 1, 2]; track i) {
                    <div class="card p-4 sm:p-5">
                      <div class="flex items-center justify-between">
                        <div class="space-y-2">
                          <div class="h-4 w-40 animate-pulse rounded bg-slate-200 dark:bg-white/10"></div>
                          <div class="h-3 w-56 animate-pulse rounded bg-slate-100 dark:bg-white/5"></div>
                        </div>
                        <div class="h-6 w-16 animate-pulse rounded-full bg-slate-100 dark:bg-white/5"></div>
                      </div>
                    </div>
                  }
                </div>
              } @else if (companies().length === 0) {
                <div class="card flex flex-col items-center gap-3 border-dashed px-6 py-14 text-center">
                  <span class="grid h-12 w-12 place-items-center rounded-full bg-slate-100 text-slate-400 dark:bg-white/5 dark:text-slate-500">
                    <ng-icon name="phosphorBuildings" class="text-2xl" />
                  </span>
                  <p class="text-sm font-medium text-slate-500 dark:text-slate-400">{{ 'admin.companies.empty' | t }}</p>
                </div>
              } @else {
                <div class="stagger-children space-y-3">
                  @for (c of companies(); track c.id) {
                    <div class="card p-4 sm:p-5">
                      <div class="flex items-start justify-between gap-3">
                        <div class="flex min-w-0 items-start gap-3">
                          <span class="mt-0.5 grid h-10 w-10 shrink-0 place-items-center rounded-2xl bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300">
                            <ng-icon name="phosphorBuildings" class="text-base" />
                          </span>
                          <div class="min-w-0">
                            <p class="truncate font-semibold text-slate-900 dark:text-white">{{ c.name }}</p>
                            <p class="truncate text-sm text-slate-500 dark:text-slate-400">{{ c.email }}{{ c.phone ? ' · ' + c.phone : '' }}</p>
                          </div>
                        </div>
                        <span class="badge shrink-0" [class]="statusBadgeClass(c.status)">{{ statusKey(c.status) | t }}</span>
                      </div>

                      <div class="mt-4 flex flex-wrap items-center gap-2 border-t border-slate-100 pt-3 dark:border-white/10">
                        @if (c.status !== 'Active') {
                          <button type="button" [disabled]="busyId() === c.id" (click)="activate(c)" class="btn btn-soft px-3.5 py-1.5 text-xs">
                            <ng-icon name="phosphorCheckCircle" class="text-sm" />
                            {{ 'admin.companies.activate' | t }}
                          </button>
                        }
                        <button type="button" (click)="toggleManager(c.id)" class="btn btn-ghost px-3.5 py-1.5 text-xs">
                          {{ (managerFor() === c.id ? 'admin.companies.close' : 'admin.companies.addManager') | t }}
                        </button>
                        <button type="button" (click)="toggleNotify(c.id)" class="btn btn-ghost px-3.5 py-1.5 text-xs">
                          {{ (notifyFor() === c.id ? 'admin.companies.close' : 'admin.companies.notify') | t }}
                        </button>
                        @if (c.status !== 'Suspended') {
                          <button type="button" [disabled]="busyId() === c.id" (click)="suspend(c)" class="btn btn-danger ms-auto px-3.5 py-1.5 text-xs">
                            {{ 'admin.companies.suspend' | t }}
                          </button>
                        }
                      </div>

                      @if (managerFor() === c.id) {
                        <form [formGroup]="managerForm" (ngSubmit)="createManager(c)" class="mt-4 space-y-3 rounded-2xl bg-slate-50 p-4 ring-1 ring-slate-100 dark:bg-white/5 dark:ring-white/10">
                          <p class="flex items-center gap-2 text-sm font-semibold text-slate-800 dark:text-slate-100">
                            <ng-icon name="phosphorUserPlus" class="text-base text-brand-600 dark:text-brand-400" />
                            {{ 'admin.companies.addManager' | t }}
                          </p>
                          <div>
                            <label for="mgrFullName" class="label">{{ 'admin.companies.managerFullName' | t }}</label>
                            <input id="mgrFullName" type="text" formControlName="fullName" [placeholder]="'admin.companies.managerFullName' | t" class="input" />
                          </div>
                          <div>
                            <label for="mgrEmail" class="label">{{ 'admin.companies.managerEmail' | t }}</label>
                            <input id="mgrEmail" type="email" formControlName="email" [placeholder]="'admin.companies.managerEmail' | t" class="input" />
                          </div>
                          <div>
                            <label for="mgrPassword" class="label">{{ 'admin.companies.tempPassword' | t }}</label>
                            <input id="mgrPassword" type="password" formControlName="password" [placeholder]="'admin.companies.tempPassword' | t" autocomplete="new-password" class="input" />
                            <p class="mt-1.5 text-xs text-slate-400 dark:text-slate-500">{{ 'admin.companies.passwordHint' | t }}</p>
                          </div>
                          <button type="submit" [disabled]="busyId() === c.id" class="btn btn-primary w-full">{{ 'admin.companies.createManager' | t }}</button>
                        </form>
                      }

                      @if (notifyFor() === c.id) {
                        <form [formGroup]="notifyForm" (ngSubmit)="notify(c)" class="mt-4 space-y-3 rounded-2xl bg-slate-50 p-4 ring-1 ring-slate-100 dark:bg-white/5 dark:ring-white/10">
                          <p class="flex items-center gap-2 text-sm font-semibold text-slate-800 dark:text-slate-100">
                            <ng-icon name="phosphorEnvelopeSimple" class="text-base text-brand-600 dark:text-brand-400" />
                            {{ 'admin.companies.notify' | t }}
                          </p>
                          <div>
                            <label for="notifyTitle" class="label">{{ 'admin.companies.notifyTitle' | t }}</label>
                            <input id="notifyTitle" type="text" formControlName="title" [placeholder]="'admin.companies.notifyTitle' | t" class="input" />
                          </div>
                          <div>
                            <label for="notifyMessage" class="label">{{ 'admin.companies.notifyMessage' | t }}</label>
                            <textarea id="notifyMessage" formControlName="message" rows="2" [placeholder]="'admin.companies.notifyMessage' | t" class="input"></textarea>
                          </div>
                          <button type="submit" [disabled]="busyId() === c.id" class="btn btn-primary w-full">{{ 'admin.companies.sendNotification' | t }}</button>
                        </form>
                      }
                    </div>
                  }
                </div>
                <p class="mt-3 text-xs text-slate-400 dark:text-slate-500">{{ 'admin.companies.count' | t: { count: total() } }}</p>
              }
            </section>

            <section class="card h-fit p-5">
              <h2 class="mb-4 flex items-center gap-2 text-base font-bold text-slate-900 dark:text-white">
                <ng-icon name="phosphorUserPlus" class="text-lg text-brand-600 dark:text-brand-400" />
                {{ 'admin.companies.onboard' | t }}
              </h2>
              <form [formGroup]="form" (ngSubmit)="create()" class="space-y-4">
                <div>
                  <label for="name" class="label">{{ 'admin.companies.name' | t }}</label>
                  <input id="name" type="text" formControlName="name" class="input" />
                </div>
                <div>
                  <label for="cemail" class="label">{{ 'admin.companies.email' | t }}</label>
                  <input id="cemail" type="email" formControlName="email" class="input" />
                </div>
                <div>
                  <label for="phone" class="label">{{ 'admin.companies.phone' | t }}</label>
                  <input id="phone" type="text" formControlName="phone" class="input" />
                </div>
                <button type="submit" [disabled]="creating()" class="btn btn-primary w-full">
                  {{ (creating() ? 'admin.companies.creating' : 'admin.companies.create') | t }}
                </button>
                <p class="text-center text-xs text-slate-400 dark:text-slate-500">{{ 'admin.companies.pendingHint' | t }}</p>
              </form>
            </section>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class AdminCompaniesComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AdminApiService);
  private readonly toasts = inject(ToastService);
  private readonly i18n = inject(TranslationService);

  protected readonly statuses = STATUSES;
  protected readonly companies = signal<Company[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly creating = signal(false);
  protected readonly busyId = signal<string | null>(null);
  protected readonly filter = signal<CompanyStatus | null>(null);
  protected readonly managerFor = signal<string | null>(null);
  protected readonly notifyFor = signal<string | null>(null);

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

  protected readonly notifyForm = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    message: ['', [Validators.required, Validators.maxLength(2000)]],
  });

  ngOnInit(): void {
    this.load();
  }

  protected statusKey(status: CompanyStatus): string {
    return STATUS_KEY[status];
  }

  /** Tailwind classes for the status badge — appended to the shared `.badge` base class. */
  protected statusBadgeClass(status: CompanyStatus): string {
    if (status === 'Active') return 'badge-brand';
    if (status === 'Pending') return 'badge-accent';
    return 'bg-rose-50 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300';
  }

  protected setFilter(status: CompanyStatus | null): void {
    this.filter.set(status);
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.listCompanies(1, 50, this.filter() ?? undefined).subscribe({
      next: (page) => {
        this.companies.set(page.data);
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
        this.toasts.success(this.i18n.t('admin.companies.toast.created', { name: body.name }));
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
        this.toasts.success(this.i18n.t('admin.companies.toast.activated', { name: c.name }));
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
        this.toasts.info(this.i18n.t('admin.companies.toast.suspended', { name: c.name }));
        this.load();
      },
      error: () => this.busyId.set(null),
    });
  }

  protected toggleManager(companyId: string): void {
    this.managerFor.set(this.managerFor() === companyId ? null : companyId);
    this.notifyFor.set(null);
    this.managerForm.reset({ fullName: '', email: '', password: '' });
  }

  protected toggleNotify(companyId: string): void {
    this.notifyFor.set(this.notifyFor() === companyId ? null : companyId);
    this.managerFor.set(null);
    this.notifyForm.reset({ title: '', message: '' });
  }

  protected notify(c: Company): void {
    if (this.notifyForm.invalid) {
      this.notifyForm.markAllAsTouched();
      return;
    }
    const v = this.notifyForm.getRawValue();
    this.busyId.set(c.id);
    this.api.notifyCompany(c.id, { title: v.title.trim(), message: v.message.trim(), type: 'info' }).subscribe({
      next: () => {
        this.busyId.set(null);
        this.notifyFor.set(null);
        this.toasts.success(this.i18n.t('admin.companies.toast.notified', { name: c.name }));
      },
      error: () => this.busyId.set(null),
    });
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
        this.toasts.success(this.i18n.t('admin.companies.toast.managerCreated', { email: m.email, name: c.name }));
      },
      error: () => this.busyId.set(null),
    });
  }
}
