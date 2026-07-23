import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorBuildings, phosphorFloppyDisk } from '@ng-icons/phosphor-icons/regular';
import { VendorApiService } from '../../core/api/vendor-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { Company } from '../../core/models';
import { VendorNavComponent } from './vendor-nav';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-vendor-company',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, VendorNavComponent, NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorBuildings, phosphorFloppyDisk })],
  template: `
    <div class="lg:grid lg:grid-cols-[15rem_1fr] lg:items-start lg:gap-8">
      <app-vendor-nav />

      <div class="min-w-0">
        <h1 class="animate-in mb-6 font-display text-2xl font-bold text-ink dark:text-white">{{ 'vendor.company.title' | t }}</h1>

        @if (loading()) {
          <p class="text-slate-500 dark:text-slate-400">{{ 'vendor.common.loading' | t }}</p>
        } @else if (company(); as c) {
          <div class="card max-w-lg p-6">
            <div class="mb-5 flex items-center gap-3 border-b border-slate-100 pb-5 dark:border-white/10">
              <span class="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300">
                <ng-icon name="phosphorBuildings" class="text-xl" aria-hidden="true" />
              </span>
              <dl class="min-w-0 flex-1 space-y-1 text-sm">
                <div class="flex items-center justify-between gap-2">
                  <dt class="text-slate-500 dark:text-slate-400">{{ 'vendor.company.email' | t }}</dt>
                  <dd class="truncate font-medium text-slate-800 dark:text-slate-100">{{ c.email }}</dd>
                </div>
                <div class="flex items-center justify-between gap-2">
                  <dt class="text-slate-500 dark:text-slate-400">{{ 'vendor.company.status' | t }}</dt>
                  <dd>
                    <span class="badge" [class]="c.status === 'Active' ? 'badge-brand' : 'badge-accent'">
                      {{ c.status }}
                    </span>
                  </dd>
                </div>
              </dl>
            </div>

            <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3.5">
              <div>
                <label for="name" class="label">{{ 'vendor.company.name' | t }}</label>
                <input id="name" type="text" formControlName="name" class="input" />
              </div>
              <div>
                <label for="phone" class="label">{{ 'vendor.company.phone' | t }}</label>
                <input id="phone" type="tel" formControlName="phone" class="input" />
              </div>
              <button type="submit" [disabled]="submitting()" class="btn btn-primary">
                <ng-icon name="phosphorFloppyDisk" aria-hidden="true" />
                {{ (submitting() ? 'vendor.common.saving' : 'vendor.common.saveChanges') | t }}
              </button>
            </form>
          </div>
        }
      </div>
    </div>
  `,
})
export class VendorCompanyComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(VendorApiService);
  private readonly toasts = inject(ToastService);
  private readonly i18n = inject(TranslationService);

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
        this.toasts.success(this.i18n.t('vendor.company.toast.updated'));
      },
      error: () => this.submitting.set(false),
    });
  }
}
