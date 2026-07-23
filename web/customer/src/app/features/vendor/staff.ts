import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CreateStaffRequest, VendorApiService } from '../../core/api/vendor-api.service';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  phosphorUsers,
  phosphorMagnifyingGlass,
  phosphorPencilSimple,
  phosphorTrash,
  phosphorCheck,
  phosphorPlus,
  phosphorFloppyDisk,
  phosphorProhibit,
  phosphorArrowsClockwise,
} from '@ng-icons/phosphor-icons/regular';
import { ToastService } from '../../core/toast/toast.service';
import { Staff, StaffType } from '../../core/models';
import { VendorNavComponent } from './vendor-nav';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

const STAFF_TYPES: StaffType[] = ['Accountant', 'Supervisor', 'Employee'];
// Map each API staff-type value to its dictionary key so the <option>/cell label can be translated
// while the underlying [value] stays the raw enum the API expects.
const STAFF_TYPE_KEY: Record<StaffType, string> = {
  Accountant: 'vendor.staff.role.accountant',
  Supervisor: 'vendor.staff.role.supervisor',
  Employee: 'vendor.staff.role.employee',
};
// Same policy as the password validators across the app: ≥10 chars with upper, lower and digit.
const STRONG_PASSWORD = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{10,}$/;

@Component({
  selector: 'app-vendor-staff',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, VendorNavComponent, NgIcon, TranslatePipe],
  providers: [
    provideIcons({
      phosphorUsers,
      phosphorMagnifyingGlass,
      phosphorPencilSimple,
      phosphorTrash,
      phosphorCheck,
      phosphorPlus,
      phosphorFloppyDisk,
      phosphorProhibit,
      phosphorArrowsClockwise,
    }),
  ],
  template: `
    <div class="lg:grid lg:grid-cols-[15rem_1fr] lg:items-start lg:gap-8">
      <app-vendor-nav />

      <div class="min-w-0">
        <h1 class="animate-in mb-6 font-display text-2xl font-bold text-ink dark:text-white">{{ 'vendor.staff.title' | t }}</h1>

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
                  [placeholder]="'vendor.staff.searchPlaceholder' | t"
                  class="input ps-10"
                />
              </div>
              <p class="text-sm text-slate-500 dark:text-slate-400">{{ 'vendor.staff.count' | t: { n: total() } }}</p>
            </div>

            @if (loading()) {
              <p class="text-slate-500 dark:text-slate-400">{{ 'vendor.common.loading' | t }}</p>
            } @else if (staff().length === 0) {
              <div class="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-slate-200 bg-slate-50/60 px-6 py-14 text-center dark:border-white/10 dark:bg-white/2">
                <span class="grid h-12 w-12 place-items-center rounded-full bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300">
                  <ng-icon name="phosphorUsers" class="text-2xl" aria-hidden="true" />
                </span>
                <p class="text-sm font-medium text-slate-600 dark:text-slate-300">{{ 'vendor.staff.empty' | t }}</p>
                <a href="#staff-form-panel" class="btn btn-primary px-4 py-2 text-sm">
                  <ng-icon name="phosphorPlus" aria-hidden="true" />
                  {{ 'vendor.staff.addStaff' | t }}
                </a>
              </div>
            } @else {
              <div class="card overflow-hidden">
                <div class="overflow-x-auto">
                  <table class="w-full text-sm">
                    <thead class="bg-slate-50 text-xs font-semibold tracking-wide text-slate-500 uppercase dark:bg-white/5 dark:text-slate-400">
                      <tr>
                        <th class="px-4 py-3 text-start font-semibold">{{ 'vendor.staff.th.name' | t }}</th>
                        <th class="px-4 py-3 text-start font-semibold">{{ 'vendor.staff.th.email' | t }}</th>
                        <th class="px-4 py-3 text-start font-semibold">{{ 'vendor.staff.th.role' | t }}</th>
                        <th class="px-4 py-3 text-start font-semibold">{{ 'vendor.staff.th.status' | t }}</th>
                        <th class="px-4 py-3"></th>
                      </tr>
                    </thead>
                    <tbody class="stagger-children divide-y divide-slate-100 dark:divide-white/10">
                      @for (s of staff(); track s.id) {
                        <tr class="transition-colors hover:bg-slate-50 dark:hover:bg-white/3">
                          <td class="px-4 py-3 font-medium text-slate-800 dark:text-slate-100">{{ s.fullName }}</td>
                          <td class="px-4 py-3 text-slate-500 dark:text-slate-400">{{ s.email }}</td>
                          <td class="px-4 py-3 text-slate-600 dark:text-slate-300">{{ roleKey(s.staffType) | t }}</td>
                          <td class="px-4 py-3">
                            <span
                              class="badge"
                              [class]="s.suspended ? 'bg-rose-50 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300' : 'badge-brand'"
                            >
                              {{ (s.suspended ? 'vendor.staff.suspended' : 'vendor.staff.active') | t }}
                            </span>
                          </td>
                          <td class="px-4 py-3 text-end whitespace-nowrap">
                            <button type="button" (click)="edit(s)" [attr.aria-label]="'vendor.common.edit' | t" class="btn btn-ghost px-2.5 py-1.5 text-xs">
                              <ng-icon name="phosphorPencilSimple" aria-hidden="true" />
                            </button>
                            @if (s.suspended) {
                              <button type="button" [disabled]="busy() === s.id" (click)="reactivate(s)" class="btn btn-soft ms-1 px-2.5 py-1.5 text-xs">
                                <ng-icon name="phosphorArrowsClockwise" aria-hidden="true" />{{ 'vendor.staff.reactivate' | t }}
                              </button>
                            } @else {
                              <button type="button" [disabled]="busy() === s.id" (click)="suspend(s)" class="btn btn-ghost ms-1 px-2.5 py-1.5 text-xs text-amber-600 hover:text-amber-700 dark:text-amber-400">
                                <ng-icon name="phosphorProhibit" aria-hidden="true" />{{ 'vendor.staff.suspend' | t }}
                              </button>
                            }
                            @if (confirmingDelete() === s.id) {
                              <button type="button" [disabled]="busy() === s.id" (click)="remove(s)" class="btn btn-danger ms-1 px-2.5 py-1.5 text-xs">
                                <ng-icon name="phosphorCheck" aria-hidden="true" />{{ 'vendor.common.confirm' | t }}
                              </button>
                              <button type="button" (click)="confirmingDelete.set(null)" class="btn btn-ghost ms-1 px-2.5 py-1.5 text-xs">{{ 'vendor.common.keep' | t }}</button>
                            } @else {
                              <button type="button" (click)="confirmingDelete.set(s.id)" [attr.aria-label]="'vendor.common.delete' | t" class="btn btn-ghost ms-1 px-2.5 py-1.5 text-xs text-slate-400 hover:text-rose-600 dark:text-slate-500">
                                <ng-icon name="phosphorTrash" aria-hidden="true" />
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

          <section id="staff-form-panel" class="card p-5">
            <h2 class="mb-3 font-display font-semibold text-ink dark:text-white">{{ (editingId() ? 'vendor.staff.editStaff' : 'vendor.staff.addStaff') | t }}</h2>
            <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3.5">
              <div>
                <label for="fullName" class="label">{{ 'vendor.staff.fullName' | t }}</label>
                <input id="fullName" type="text" formControlName="fullName" class="input" />
              </div>
              @if (!editingId()) {
                <div>
                  <label for="email" class="label">{{ 'vendor.staff.email' | t }}</label>
                  <input id="email" type="email" formControlName="email" class="input" />
                </div>
                <div>
                  <label for="password" class="label">{{ 'vendor.staff.tempPassword' | t }}</label>
                  <input id="password" type="password" formControlName="password" class="input" />
                  <p class="mt-1 text-xs text-slate-400 dark:text-slate-500">{{ 'vendor.staff.passwordHint' | t }}</p>
                </div>
              }
              <div>
                <label for="staffType" class="label">{{ 'vendor.staff.role' | t }}</label>
                <select id="staffType" formControlName="staffType" class="input">
                  @for (t of staffTypes; track t) {
                    <option [value]="t">{{ roleKey(t) | t }}</option>
                  }
                </select>
              </div>
              <button type="submit" [disabled]="submitting()" class="btn btn-primary w-full">
                <ng-icon [name]="editingId() ? 'phosphorFloppyDisk' : 'phosphorPlus'" aria-hidden="true" />
                {{ (submitting() ? 'vendor.common.saving' : editingId() ? 'vendor.common.saveChanges' : 'vendor.staff.addStaffBtn') | t }}
              </button>
              @if (editingId()) {
                <button type="button" (click)="cancelEdit()" class="btn btn-ghost w-full">{{ 'vendor.common.cancelEdit' | t }}</button>
              }
            </form>
          </section>
        </div>
      </div>
    </div>
  `,
})
export class VendorStaffComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(VendorApiService);
  private readonly toasts = inject(ToastService);
  private readonly i18n = inject(TranslationService);

  protected readonly staffTypes = STAFF_TYPES;
  /** Dictionary key for a staff role, so the template can translate the label. */
  protected roleKey(type: string): string {
    return STAFF_TYPE_KEY[type as StaffType] ?? type;
  }
  protected readonly staff = signal<Staff[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly busy = signal<string | null>(null);
  protected readonly editingId = signal<string | null>(null);
  protected readonly confirmingDelete = signal<string | null>(null);
  protected readonly search = signal('');
  private searchTimer?: ReturnType<typeof setTimeout>;

  protected readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.pattern(STRONG_PASSWORD)]],
    staffType: ['Employee' as StaffType, [Validators.required]],
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
    this.api.listStaff(1, 50, this.search().trim() || undefined).subscribe({
      next: (page) => {
        this.staff.set(page.data);
        this.total.set(page.total);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected edit(s: Staff): void {
    this.editingId.set(s.id);
    this.form.controls.email.disable();
    this.form.controls.password.disable();
    this.form.patchValue({ fullName: s.fullName, staffType: s.staffType as StaffType });
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
    this.form.controls.email.enable();
    this.form.controls.password.enable();
    this.form.reset({ fullName: '', email: '', password: '', staffType: 'Employee' });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toasts.info(this.i18n.t('vendor.staff.toast.completeForm'));
      return;
    }
    const v = this.form.getRawValue();
    this.submitting.set(true);
    const editId = this.editingId();
    if (editId) {
      this.api.updateStaff(editId, { fullName: v.fullName.trim(), staffType: v.staffType }).subscribe({
        next: () => {
          this.submitting.set(false);
          this.toasts.success(this.i18n.t('vendor.staff.toast.updated'));
          this.cancelEdit();
          this.load();
        },
        error: () => this.submitting.set(false),
      });
    } else {
      const body: CreateStaffRequest = {
        fullName: v.fullName.trim(),
        email: v.email.trim().toLowerCase(),
        password: v.password,
        staffType: v.staffType,
      };
      this.api.createStaff(body).subscribe({
        next: () => {
          this.submitting.set(false);
          this.toasts.success(this.i18n.t('vendor.staff.toast.created', { email: body.email }));
          this.cancelEdit();
          this.load();
        },
        error: () => this.submitting.set(false),
      });
    }
  }

  protected suspend(s: Staff): void {
    this.busy.set(s.id);
    this.api.suspendStaff(s.id).subscribe({
      next: () => { this.busy.set(null); this.toasts.success(this.i18n.t('vendor.staff.toast.suspended', { name: s.fullName })); this.load(); },
      error: () => this.busy.set(null),
    });
  }

  protected reactivate(s: Staff): void {
    this.busy.set(s.id);
    this.api.reactivateStaff(s.id).subscribe({
      next: () => { this.busy.set(null); this.toasts.success(this.i18n.t('vendor.staff.toast.reactivated', { name: s.fullName })); this.load(); },
      error: () => this.busy.set(null),
    });
  }

  protected remove(s: Staff): void {
    this.busy.set(s.id);
    this.api.deleteStaff(s.id).subscribe({
      next: () => { this.busy.set(null); this.confirmingDelete.set(null); this.toasts.success(this.i18n.t('vendor.staff.toast.deleted', { name: s.fullName })); this.load(); },
      error: () => { this.busy.set(null); this.confirmingDelete.set(null); },
    });
  }
}
