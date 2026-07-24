import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorTag, phosphorProhibit, phosphorPlus } from '@ng-icons/phosphor-icons/regular';
import { CreatePromoCodeRequest, VendorApiService } from '../../core/api/vendor-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { DiscountType, PromoCodeDto } from '../../core/models';
import { VendorNavComponent } from './vendor-nav';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-vendor-promo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, VendorNavComponent, NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorTag, phosphorProhibit, phosphorPlus })],
  template: `
    <div class="lg:grid lg:grid-cols-[15rem_1fr] lg:items-start lg:gap-8">
      <app-vendor-nav />

      <div class="min-w-0">
        <h1 class="animate-in mb-6 font-display text-2xl font-bold text-ink dark:text-white">{{ 'vendor.promo.title' | t }}</h1>

        <div class="grid gap-6 lg:grid-cols-3">
          <section class="lg:col-span-2">
            <div class="mb-3 flex items-center justify-between gap-3">
              <p class="text-sm text-slate-500 dark:text-slate-400">{{ 'vendor.promo.count' | t: { n: total() } }}</p>
            </div>

            @if (loading()) {
              <p class="text-slate-500 dark:text-slate-400">{{ 'vendor.common.loading' | t }}</p>
            } @else if (codes().length === 0) {
              <div class="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-slate-200 bg-slate-50/60 px-6 py-14 text-center dark:border-white/10 dark:bg-white/5">
                <span class="grid h-12 w-12 place-items-center rounded-full bg-accent-300/30 text-accent-600 dark:bg-accent-400/15 dark:text-accent-300">
                  <ng-icon name="phosphorTag" class="text-2xl" aria-hidden="true" />
                </span>
                <p class="text-sm font-medium text-slate-600 dark:text-slate-300">{{ 'vendor.promo.empty' | t }}</p>
                <a href="#promo-form-panel" class="btn btn-primary px-4 py-2 text-sm">
                  <ng-icon name="phosphorPlus" aria-hidden="true" />
                  {{ 'vendor.promo.createCode' | t }}
                </a>
              </div>
            } @else {
              <div class="card overflow-hidden">
                <div class="overflow-x-auto">
                  <table class="w-full text-sm">
                    <thead class="bg-slate-50 text-xs font-semibold tracking-wide text-slate-500 uppercase dark:bg-white/5 dark:text-slate-400">
                      <tr>
                        <th class="px-3 py-3 text-start font-semibold">{{ 'vendor.promo.th.code' | t }}</th>
                        <th class="px-3 py-3 text-end font-semibold">{{ 'vendor.promo.th.discount' | t }}</th>
                        <th class="px-3 py-3 text-end font-semibold">{{ 'vendor.promo.th.used' | t }}</th>
                        <th class="px-3 py-3 text-start font-semibold">{{ 'vendor.promo.th.expires' | t }}</th>
                        <th class="px-3 py-3 text-start font-semibold">{{ 'vendor.promo.th.status' | t }}</th>
                        <th class="px-3 py-3"></th>
                      </tr>
                    </thead>
                    <tbody class="stagger-children divide-y divide-slate-100 dark:divide-white/10">
                      @for (c of codes(); track c.id) {
                        <tr class="transition-colors hover:bg-slate-50 dark:hover:bg-white/3">
                          <td class="px-3 py-3 font-mono font-medium text-slate-800 dark:text-slate-100">{{ c.code }}</td>
                          <td class="px-3 py-3 text-end tabular-nums text-slate-700 dark:text-slate-200">
                            {{ c.discountType === 'Percent' ? (c.discountValue | number: '1.0-0') + '%' : (c.discountValue | number: '1.0-0') + ' ' + ('vendor.promo.off' | t) }}
                          </td>
                          <td class="px-3 py-3 text-end tabular-nums text-slate-600 dark:text-slate-300">{{ c.redemptionCount }}{{ c.maxRedemptions ? ' / ' + c.maxRedemptions : '' }}</td>
                          <td class="px-3 py-3 tabular-nums text-slate-500 dark:text-slate-400">{{ c.expiresAtUtc ? (c.expiresAtUtc | date: 'MMM d, y') : '—' }}</td>
                          <td class="px-3 py-3">
                            <span class="badge" [class]="c.active ? 'badge-brand' : 'badge-muted'">
                              {{ (c.active ? 'vendor.common.active' : 'vendor.common.inactive') | t }}
                            </span>
                          </td>
                          <td class="px-3 py-3 text-end">
                            @if (c.active) {
                              <button type="button" [disabled]="busy() === c.id" (click)="deactivate(c)" class="btn btn-ghost px-2.5 py-1.5 text-xs text-rose-600 hover:text-rose-700 dark:text-rose-400">
                                <ng-icon name="phosphorProhibit" aria-hidden="true" />{{ 'vendor.promo.deactivate' | t }}
                              </button>
                            }
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            }
          </section>

          <section id="promo-form-panel" class="card p-5">
            <h2 class="mb-3 font-display font-semibold text-ink dark:text-white">{{ 'vendor.promo.createCode' | t }}</h2>
            <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3.5">
              <div>
                <label for="code" class="label">{{ 'vendor.promo.code' | t }}</label>
                <input id="code" type="text" formControlName="code" class="input uppercase" />
              </div>
              <div>
                <label for="discountType" class="label">{{ 'vendor.promo.type' | t }}</label>
                <select id="discountType" formControlName="discountType" class="input">
                  <option value="Percent">{{ 'vendor.promo.typePercent' | t }}</option>
                  <option value="Fixed">{{ 'vendor.promo.typeFixed' | t }}</option>
                </select>
              </div>
              <div>
                <label for="discountValue" class="label">
                  {{ (form.controls.discountType.value === 'Percent' ? 'vendor.promo.percentOff' : 'vendor.promo.amountOff') | t }}
                </label>
                <input id="discountValue" type="number" min="1" formControlName="discountValue" class="input" />
              </div>
              <div>
                <label for="maxRedemptions" class="label">{{ 'vendor.promo.maxUses' | t }}</label>
                <input id="maxRedemptions" type="number" min="1" formControlName="maxRedemptions" class="input" />
              </div>
              <div>
                <label for="expires" class="label">{{ 'vendor.promo.expires' | t }}</label>
                <input id="expires" type="date" formControlName="expires" class="input" />
              </div>
              <button type="submit" [disabled]="submitting()" class="btn btn-primary w-full">
                <ng-icon name="phosphorPlus" aria-hidden="true" />
                {{ (submitting() ? 'vendor.promo.creating' : 'vendor.promo.createCodeBtn') | t }}
              </button>
            </form>
          </section>
        </div>
      </div>
    </div>
  `,
})
export class VendorPromoComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(VendorApiService);
  private readonly toasts = inject(ToastService);
  private readonly i18n = inject(TranslationService);

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
        this.toasts.success(this.i18n.t('vendor.promo.toast.created', { code: body.code }));
        this.form.reset({ code: '', discountType: 'Percent', discountValue: 10, maxRedemptions: null, expires: '' });
        this.load();
      },
      error: () => this.submitting.set(false),
    });
  }

  protected deactivate(c: PromoCodeDto): void {
    this.busy.set(c.id);
    this.api.deactivatePromoCode(c.id).subscribe({
      next: () => { this.busy.set(null); this.toasts.success(this.i18n.t('vendor.promo.toast.deactivated', { code: c.code })); this.load(); },
      error: () => this.busy.set(null),
    });
  }
}
