import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CreatePromoCodeRequest, VendorApiService } from '../../core/api/vendor-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { DiscountType, PromoCodeDto } from '../../core/models';
import { VendorNavComponent } from './vendor-nav';

@Component({
  selector: 'app-vendor-promo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, VendorNavComponent],
  template: `
    <app-vendor-nav />
    <h1 class="mb-6 text-2xl font-bold text-slate-900">Promo codes</h1>

    <div class="grid gap-6 lg:grid-cols-3">
      <section class="lg:col-span-2">
        @if (loading()) {
          <p class="text-slate-500">Loading…</p>
        } @else if (codes().length === 0) {
          <p class="rounded-lg bg-slate-100 p-4 text-slate-600">No promo codes yet.</p>
        } @else {
          <div class="overflow-x-auto rounded-xl border border-slate-200 bg-white">
            <table class="w-full text-sm">
              <thead class="bg-slate-50 text-left text-slate-500">
                <tr>
                  <th class="px-3 py-2 font-medium">Code</th>
                  <th class="px-3 py-2 font-medium">Discount</th>
                  <th class="px-3 py-2 font-medium">Used</th>
                  <th class="px-3 py-2 font-medium">Expires</th>
                  <th class="px-3 py-2 font-medium">Status</th>
                  <th class="px-3 py-2 font-medium"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                @for (c of codes(); track c.id) {
                  <tr>
                    <td class="px-3 py-2 font-mono font-medium text-slate-800">{{ c.code }}</td>
                    <td class="px-3 py-2">
                      {{ c.discountType === 'Percent' ? (c.discountValue | number: '1.0-0') + '%' : (c.discountValue | number: '1.0-0') + ' off' }}
                    </td>
                    <td class="px-3 py-2">{{ c.redemptionCount }}{{ c.maxRedemptions ? ' / ' + c.maxRedemptions : '' }}</td>
                    <td class="px-3 py-2 text-slate-500">{{ c.expiresAtUtc ? (c.expiresAtUtc | date: 'MMM d, y') : '—' }}</td>
                    <td class="px-3 py-2">
                      <span class="rounded-full px-2 py-0.5 text-xs font-semibold"
                        [class.bg-emerald-100]="c.active" [class.text-emerald-700]="c.active"
                        [class.bg-slate-200]="!c.active" [class.text-slate-600]="!c.active">
                        {{ c.active ? 'Active' : 'Inactive' }}
                      </span>
                    </td>
                    <td class="px-3 py-2 text-right">
                      @if (c.active) {
                        <button type="button" [disabled]="busy() === c.id" (click)="deactivate(c)" class="text-sm font-medium text-rose-600 hover:text-rose-700">Deactivate</button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <p class="mt-2 text-xs text-slate-400">{{ total() }} code(s)</p>
        }
      </section>

      <section class="rounded-xl border border-slate-200 bg-white p-4">
        <h2 class="mb-3 font-semibold text-slate-900">Create a code</h2>
        <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3">
          <div>
            <label for="code" class="mb-1 block text-sm font-medium text-slate-700">Code</label>
            <input id="code" type="text" formControlName="code" class="w-full rounded-md border border-slate-300 px-3 py-2 uppercase focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
          </div>
          <div>
            <label for="discountType" class="mb-1 block text-sm font-medium text-slate-700">Type</label>
            <select id="discountType" formControlName="discountType" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500">
              <option value="Percent">Percentage</option>
              <option value="Fixed">Fixed amount</option>
            </select>
          </div>
          <div>
            <label for="discountValue" class="mb-1 block text-sm font-medium text-slate-700">
              {{ form.controls.discountType.value === 'Percent' ? 'Percent off (1–100)' : 'Amount off' }}
            </label>
            <input id="discountValue" type="number" min="1" formControlName="discountValue" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
          </div>
          <div>
            <label for="maxRedemptions" class="mb-1 block text-sm font-medium text-slate-700">Max uses (optional)</label>
            <input id="maxRedemptions" type="number" min="1" formControlName="maxRedemptions" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
          </div>
          <div>
            <label for="expires" class="mb-1 block text-sm font-medium text-slate-700">Expires (optional)</label>
            <input id="expires" type="date" formControlName="expires" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
          </div>
          <button type="submit" [disabled]="submitting()" class="w-full rounded-md bg-brand-600 px-4 py-2 font-medium text-white hover:bg-brand-700 disabled:opacity-50">
            {{ submitting() ? 'Creating…' : 'Create code' }}
          </button>
        </form>
      </section>
    </div>
  `,
})
export class VendorPromoComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(VendorApiService);
  private readonly toasts = inject(ToastService);

  protected readonly codes = signal<PromoCodeDto[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly busy = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(40)]],
    discountType: ['Percent' as DiscountType, [Validators.required]],
    discountValue: [10, [Validators.required, Validators.min(1)]],
    maxRedemptions: [null as number | null],
    expires: [''],
  });

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.listPromoCodes().subscribe({
      next: (page) => {
        this.codes.set(page.data);
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
    const body: CreatePromoCodeRequest = {
      code: v.code.trim().toUpperCase(),
      discountType: v.discountType,
      discountValue: v.discountValue,
      maxRedemptions: v.maxRedemptions ? Number(v.maxRedemptions) : null,
      expiresAtUtc: v.expires ? new Date(v.expires).toISOString() : null,
    };
    this.submitting.set(true);
    this.api.createPromoCode(body).subscribe({
      next: () => {
        this.submitting.set(false);
        this.toasts.success(`Promo code ${body.code} created.`);
        this.form.reset({ code: '', discountType: 'Percent', discountValue: 10, maxRedemptions: null, expires: '' });
        this.load();
      },
      error: () => this.submitting.set(false),
    });
  }

  protected deactivate(c: PromoCodeDto): void {
    this.busy.set(c.id);
    this.api.deactivatePromoCode(c.id).subscribe({
      next: () => { this.busy.set(null); this.toasts.success(`${c.code} deactivated.`); this.load(); },
      error: () => this.busy.set(null),
    });
  }
}
