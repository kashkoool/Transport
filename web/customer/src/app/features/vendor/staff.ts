import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CreateStaffRequest, VendorApiService } from '../../core/api/vendor-api.service';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
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
  imports: [ReactiveFormsModule, VendorNavComponent, TranslatePipe],
  template: `
    <app-vendor-nav />
    <h1 class="mb-6 text-2xl font-bold text-slate-900">{{ 'vendor.staff.title' | t }}</h1>

    <div class="grid gap-6 lg:grid-cols-3">
      <section class="lg:col-span-2">
        <input
          type="search"
          [value]="search()"
          (input)="onSearch($event)"
          [placeholder]="'vendor.staff.searchPlaceholder' | t"
          class="mb-3 w-full max-w-sm rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
        />

        @if (loading()) {
          <p class="text-slate-500">{{ 'vendor.common.loading' | t }}</p>
        } @else if (staff().length === 0) {
          <p class="rounded-lg bg-slate-100 p-4 text-slate-600">{{ 'vendor.staff.empty' | t }}</p>
        } @else {
          <div class="overflow-x-auto rounded-xl border border-slate-200 bg-white">
            <table class="w-full text-sm">
              <thead class="bg-slate-50 text-left text-slate-500">
                <tr>
                  <th class="px-4 py-2 font-medium">{{ 'vendor.staff.th.name' | t }}</th>
                  <th class="px-4 py-2 font-medium">{{ 'vendor.staff.th.email' | t }}</th>
                  <th class="px-4 py-2 font-medium">{{ 'vendor.staff.th.role' | t }}</th>
                  <th class="px-4 py-2 font-medium">{{ 'vendor.staff.th.status' | t }}</th>
                  <th class="px-4 py-2 font-medium"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                @for (s of staff(); track s.id) {
                  <tr>
                    <td class="px-4 py-2 font-medium text-slate-800">{{ s.fullName }}</td>
                    <td class="px-4 py-2 text-slate-500">{{ s.email }}</td>
                    <td class="px-4 py-2">{{ roleKey(s.staffType) | t }}</td>
                    <td class="px-4 py-2">
                      <span class="rounded-full px-2 py-0.5 text-xs font-semibold"
                        [class.bg-emerald-100]="!s.suspended" [class.text-emerald-700]="!s.suspended"
                        [class.bg-rose-100]="s.suspended" [class.text-rose-700]="s.suspended">
                        {{ (s.suspended ? 'vendor.staff.suspended' : 'vendor.staff.active') | t }}
                      </span>
                    </td>
                    <td class="px-4 py-2 text-right whitespace-nowrap">
                      <button type="button" (click)="edit(s)" class="text-sm font-medium text-brand-600 hover:text-brand-700">{{ 'vendor.common.edit' | t }}</button>
                      @if (s.suspended) {
                        <button type="button" [disabled]="busy() === s.id" (click)="reactivate(s)" class="ml-3 text-sm font-medium text-emerald-600 hover:text-emerald-700">{{ 'vendor.staff.reactivate' | t }}</button>
                      } @else {
                        <button type="button" [disabled]="busy() === s.id" (click)="suspend(s)" class="ml-3 text-sm font-medium text-amber-600 hover:text-amber-700">{{ 'vendor.staff.suspend' | t }}</button>
                      }
                      @if (confirmingDelete() === s.id) {
                        <button type="button" [disabled]="busy() === s.id" (click)="remove(s)" class="ml-3 text-sm font-medium text-rose-700">{{ 'vendor.common.confirm' | t }}</button>
                        <button type="button" (click)="confirmingDelete.set(null)" class="ml-2 text-sm text-slate-500">{{ 'vendor.common.keep' | t }}</button>
                      } @else {
                        <button type="button" (click)="confirmingDelete.set(s.id)" class="ml-3 text-sm font-medium text-rose-600 hover:text-rose-700">{{ 'vendor.common.delete' | t }}</button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <p class="mt-2 text-xs text-slate-400">{{ 'vendor.staff.count' | t: { n: total() } }}</p>
        }
      </section>

      <section class="rounded-xl border border-slate-200 bg-white p-4">
        <h2 class="mb-3 font-semibold text-slate-900">{{ (editingId() ? 'vendor.staff.editStaff' : 'vendor.staff.addStaff') | t }}</h2>
        <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3">
          <div>
            <label for="fullName" class="mb-1 block text-sm font-medium text-slate-700">{{ 'vendor.staff.fullName' | t }}</label>
            <input id="fullName" type="text" formControlName="fullName" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
          </div>
          @if (!editingId()) {
            <div>
              <label for="email" class="mb-1 block text-sm font-medium text-slate-700">{{ 'vendor.staff.email' | t }}</label>
              <input id="email" type="email" formControlName="email" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
            </div>
            <div>
              <label for="password" class="mb-1 block text-sm font-medium text-slate-700">{{ 'vendor.staff.tempPassword' | t }}</label>
              <input id="password" type="password" formControlName="password" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
              <p class="mt-1 text-xs text-slate-400">{{ 'vendor.staff.passwordHint' | t }}</p>
            </div>
          }
          <div>
            <label for="staffType" class="mb-1 block text-sm font-medium text-slate-700">{{ 'vendor.staff.role' | t }}</label>
            <select id="staffType" formControlName="staffType" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500">
              @for (t of staffTypes; track t) {
                <option [value]="t">{{ roleKey(t) | t }}</option>
              }
            </select>
          </div>
          <button type="submit" [disabled]="submitting()" class="w-full rounded-md bg-brand-600 px-4 py-2 font-medium text-white hover:bg-brand-700 disabled:opacity-50">
            {{ (submitting() ? 'vendor.common.saving' : editingId() ? 'vendor.common.saveChanges' : 'vendor.staff.addStaffBtn') | t }}
          </button>
          @if (editingId()) {
            <button type="button" (click)="cancelEdit()" class="w-full text-sm text-slate-500 hover:text-slate-700">{{ 'vendor.common.cancelEdit' | t }}</button>
          }
        </form>
      </section>
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
