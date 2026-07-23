import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorSteeringWheel, phosphorMagnifyingGlass, phosphorPlus } from '@ng-icons/phosphor-icons/regular';
import { AddDriverRequest, VendorApiService } from '../../core/api/vendor-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { Driver } from '../../core/models';
import { VendorNavComponent } from './vendor-nav';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-vendor-drivers',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, VendorNavComponent, NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorSteeringWheel, phosphorMagnifyingGlass, phosphorPlus })],
  template: `
    <div class="lg:grid lg:grid-cols-[15rem_1fr] lg:items-start lg:gap-8">
      <app-vendor-nav />

      <div class="min-w-0">
        <h1 class="animate-in mb-6 font-display text-2xl font-bold text-ink dark:text-white">{{ 'vendor.drivers.title' | t }}</h1>

        <div class="grid gap-6 lg:grid-cols-3">
          <section class="lg:col-span-2">
            <div class="mb-3 flex flex-wrap items-center justify-between gap-3">
              <div class="relative w-full max-w-sm">
                <span class="pointer-events-none absolute inset-y-0 inset-s-3 flex items-center text-slate-400">
                  <ng-icon name="phosphorMagnifyingGlass" aria-hidden="true" />
                </span>
                <input
                  type="search"
                  [value]="search()"
                  (input)="onSearch($event)"
                  [placeholder]="'vendor.drivers.searchPlaceholder' | t"
                  class="input ps-10"
                />
              </div>
              <p class="text-sm text-slate-500 dark:text-slate-400">{{ 'vendor.drivers.count' | t: { n: total() } }}</p>
            </div>

            @if (loading()) {
              <p class="text-slate-500 dark:text-slate-400">{{ 'vendor.common.loading' | t }}</p>
            } @else if (drivers().length === 0) {
              <div class="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-slate-200 bg-slate-50/60 px-6 py-14 text-center dark:border-white/10 dark:bg-white/5">
                <span class="grid h-12 w-12 place-items-center rounded-full bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300">
                  <ng-icon name="phosphorSteeringWheel" class="text-2xl" aria-hidden="true" />
                </span>
                <p class="text-sm font-medium text-slate-600 dark:text-slate-300">{{ 'vendor.drivers.empty' | t }}</p>
                <a href="#driver-form-panel" class="btn btn-primary px-4 py-2 text-sm">
                  <ng-icon name="phosphorPlus" aria-hidden="true" />
                  {{ 'vendor.drivers.addDriver' | t }}
                </a>
              </div>
            } @else {
              <div class="card overflow-hidden">
                <div class="overflow-x-auto">
                  <table class="w-full text-sm">
                    <thead class="bg-slate-50 text-xs font-semibold tracking-wide text-slate-500 uppercase dark:bg-white/5 dark:text-slate-400">
                      <tr>
                        <th class="px-4 py-3 text-start font-semibold">{{ 'vendor.drivers.th.name' | t }}</th>
                        <th class="px-4 py-3 text-start font-semibold">{{ 'vendor.drivers.th.phone' | t }}</th>
                        <th class="px-4 py-3 text-start font-semibold">{{ 'vendor.drivers.th.license' | t }}</th>
                      </tr>
                    </thead>
                    <tbody class="stagger-children divide-y divide-slate-100 dark:divide-white/10">
                      @for (d of drivers(); track d.id) {
                        <tr class="transition-colors hover:bg-slate-50 dark:hover:bg-white/3">
                          <td class="px-4 py-3 font-medium text-slate-800 dark:text-slate-100">{{ d.fullName }}</td>
                          <td class="px-4 py-3 tabular-nums text-slate-500 dark:text-slate-400">{{ d.phone || '—' }}</td>
                          <td class="px-4 py-3 tabular-nums text-slate-500 dark:text-slate-400">{{ d.licenseNumber || '—' }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            }
          </section>

          <section id="driver-form-panel" class="card p-5">
            <h2 class="mb-3 font-display font-semibold text-ink dark:text-white">{{ 'vendor.drivers.addDriver' | t }}</h2>
            <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3.5">
              <div>
                <label for="fullName" class="label">{{ 'vendor.drivers.fullName' | t }}</label>
                <input id="fullName" type="text" formControlName="fullName" class="input" />
              </div>
              <div>
                <label for="phone" class="label">{{ 'vendor.drivers.phone' | t }}</label>
                <input id="phone" type="tel" formControlName="phone" class="input" />
              </div>
              <div>
                <label for="licenseNumber" class="label">{{ 'vendor.drivers.license' | t }}</label>
                <input id="licenseNumber" type="text" formControlName="licenseNumber" class="input" />
              </div>
              <button type="submit" [disabled]="submitting()" class="btn btn-primary w-full">
                <ng-icon name="phosphorPlus" aria-hidden="true" />
                {{ (submitting() ? 'vendor.drivers.adding' : 'vendor.drivers.addDriverBtn') | t }}
              </button>
            </form>
          </section>
        </div>
      </div>
    </div>
  `,
})
export class VendorDriversComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(VendorApiService);
  private readonly toasts = inject(ToastService);
  private readonly i18n = inject(TranslationService);

  protected readonly drivers = signal<Driver[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly search = signal('');
  private searchTimer?: ReturnType<typeof setTimeout>;

  protected readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(100)]],
    phone: ['', [Validators.maxLength(30)]],
    licenseNumber: ['', [Validators.maxLength(40)]],
  });

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
    this.api.listDrivers(1, 100, this.search().trim() || undefined).subscribe({
      next: (page) => {
        this.drivers.set(page.data);
        this.total.set(page.total);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body: AddDriverRequest = {
      fullName: v.fullName.trim(),
      phone: v.phone.trim() || null,
      licenseNumber: v.licenseNumber.trim() || null,
    };
    this.submitting.set(true);
    this.api.addDriver(body).subscribe({
      next: () => {
        this.submitting.set(false);
        this.toasts.success(this.i18n.t('vendor.drivers.toast.added', { name: body.fullName }));
        this.form.reset({ fullName: '', phone: '', licenseNumber: '' });
        this.load();
      },
      error: () => this.submitting.set(false),
    });
  }
}
