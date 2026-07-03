import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { VendorApiService } from '../../core/api/vendor-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { Company } from '../../core/models';
import { VendorNavComponent } from './vendor-nav';

@Component({
  selector: 'app-vendor-company',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, VendorNavComponent],
  template: `
    <app-vendor-nav />
    <h1 class="mb-6 text-2xl font-bold text-slate-900">Company profile</h1>

    @if (loading()) {
      <p class="text-slate-500">Loading…</p>
    } @else if (company(); as c) {
      <div class="max-w-lg rounded-xl border border-slate-200 bg-white p-5">
        <dl class="mb-4 space-y-1 text-sm">
          <div class="flex justify-between"><dt class="text-slate-500">Email</dt><dd class="font-medium text-slate-800">{{ c.email }}</dd></div>
          <div class="flex justify-between">
            <dt class="text-slate-500">Status</dt>
            <dd>
              <span class="rounded-full px-2 py-0.5 text-xs font-semibold"
                [class.bg-emerald-100]="c.status === 'Active'" [class.text-emerald-700]="c.status === 'Active'"
                [class.bg-amber-100]="c.status !== 'Active'" [class.text-amber-700]="c.status !== 'Active'">
                {{ c.status }}
              </span>
            </dd>
          </div>
        </dl>

        <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3 border-t border-slate-100 pt-4">
          <div>
            <label for="name" class="mb-1 block text-sm font-medium text-slate-700">Company name</label>
            <input id="name" type="text" formControlName="name" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
          </div>
          <div>
            <label for="phone" class="mb-1 block text-sm font-medium text-slate-700">Phone (optional)</label>
            <input id="phone" type="tel" formControlName="phone" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
          </div>
          <button type="submit" [disabled]="submitting()" class="rounded-md bg-brand-600 px-5 py-2 font-medium text-white hover:bg-brand-700 disabled:opacity-50">
            {{ submitting() ? 'Saving…' : 'Save changes' }}
          </button>
        </form>
      </div>
    }
  `,
})
export class VendorCompanyComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(VendorApiService);
  private readonly toasts = inject(ToastService);

  protected readonly company = signal<Company | null>(null);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    phone: ['', [Validators.maxLength(30)]],
  });

  ngOnInit(): void {
    this.api.getCompany().subscribe({
      next: (c) => {
        this.company.set(c);
        this.form.reset({ name: c.name, phone: c.phone ?? '' });
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
    this.submitting.set(true);
    this.api.updateCompany({ name: v.name.trim(), phone: v.phone.trim() || null }).subscribe({
      next: (c) => {
        this.submitting.set(false);
        this.company.set(c);
        this.toasts.success('Company profile updated.');
      },
      error: () => this.submitting.set(false),
    });
  }
}
