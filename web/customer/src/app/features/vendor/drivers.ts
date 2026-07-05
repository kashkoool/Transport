import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AddDriverRequest, VendorApiService } from '../../core/api/vendor-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { Driver } from '../../core/models';
import { VendorNavComponent } from './vendor-nav';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-vendor-drivers',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, VendorNavComponent, TranslatePipe],
  template: `
    <app-vendor-nav />
    <h1 class="mb-6 text-2xl font-bold text-slate-900">{{ 'vendor.drivers.title' | t }}</h1>

    <div class="grid gap-6 lg:grid-cols-3">
      <section class="lg:col-span-2">
        <input
          type="search"
          [value]="search()"
          (input)="onSearch($event)"
          [placeholder]="'vendor.drivers.searchPlaceholder' | t"
          class="mb-3 w-full max-w-sm rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
        />
        @if (loading()) {
          <p class="text-slate-500">{{ 'vendor.common.loading' | t }}</p>
        } @else if (drivers().length === 0) {
          <p class="rounded-lg bg-slate-100 p-4 text-slate-600">{{ 'vendor.drivers.empty' | t }}</p>
        } @else {
          <div class="overflow-x-auto">
          <table class="w-full overflow-hidden rounded-xl border border-slate-200 bg-white text-sm">
            <thead class="bg-slate-50 text-left text-slate-500">
              <tr>
                <th class="px-4 py-2 font-medium">{{ 'vendor.drivers.th.name' | t }}</th>
                <th class="px-4 py-2 font-medium">{{ 'vendor.drivers.th.phone' | t }}</th>
                <th class="px-4 py-2 font-medium">{{ 'vendor.drivers.th.license' | t }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100">
              @for (d of drivers(); track d.id) {
                <tr>
                  <td class="px-4 py-2 font-medium text-slate-800">{{ d.fullName }}</td>
                  <td class="px-4 py-2 text-slate-500">{{ d.phone || '—' }}</td>
                  <td class="px-4 py-2 text-slate-500">{{ d.licenseNumber || '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
          </div>
          <p class="mt-2 text-xs text-slate-400">{{ 'vendor.drivers.count' | t: { n: total() } }}</p>
        }
      </section>

      <section class="rounded-xl border border-slate-200 bg-white p-4">
        <h2 class="mb-3 font-semibold text-slate-900">{{ 'vendor.drivers.addDriver' | t }}</h2>
        <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3">
          <div>
            <label for="fullName" class="mb-1 block text-sm font-medium text-slate-700">{{ 'vendor.drivers.fullName' | t }}</label>
            <input id="fullName" type="text" formControlName="fullName" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
          </div>
          <div>
            <label for="phone" class="mb-1 block text-sm font-medium text-slate-700">{{ 'vendor.drivers.phone' | t }}</label>
            <input id="phone" type="tel" formControlName="phone" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
          </div>
          <div>
            <label for="licenseNumber" class="mb-1 block text-sm font-medium text-slate-700">{{ 'vendor.drivers.license' | t }}</label>
            <input id="licenseNumber" type="text" formControlName="licenseNumber" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
          </div>
          <button type="submit" [disabled]="submitting()" class="w-full rounded-md bg-brand-600 px-4 py-2 font-medium text-white hover:bg-brand-700 disabled:opacity-50">
            {{ (submitting() ? 'vendor.drivers.adding' : 'vendor.drivers.addDriverBtn') | t }}
          </button>
        </form>
      </section>
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
