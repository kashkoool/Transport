import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CreateStaffRequest, VendorApiService } from '../../core/api/vendor-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { Staff, StaffType } from '../../core/models';
import { VendorNavComponent } from './vendor-nav';

const STAFF_TYPES: StaffType[] = ['Accountant', 'Supervisor', 'Employee'];
// Same policy as the password validators across the app: ≥10 chars with upper, lower and digit.
const STRONG_PASSWORD = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{10,}$/;

@Component({
  selector: 'app-vendor-staff',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, VendorNavComponent],
  template: `
    <app-vendor-nav />
    <h1 class="mb-6 text-2xl font-bold text-slate-900">Staff</h1>

    <div class="grid gap-6 lg:grid-cols-3">
      <section class="lg:col-span-2">
        @if (loading()) {
          <p class="text-slate-500">Loading…</p>
        } @else if (staff().length === 0) {
          <p class="rounded-lg bg-slate-100 p-4 text-slate-600">No staff accounts yet.</p>
        } @else {
          <div class="overflow-x-auto rounded-xl border border-slate-200 bg-white">
            <table class="w-full text-sm">
              <thead class="bg-slate-50 text-left text-slate-500">
                <tr>
                  <th class="px-4 py-2 font-medium">Name</th>
                  <th class="px-4 py-2 font-medium">Email</th>
                  <th class="px-4 py-2 font-medium">Role</th>
                  <th class="px-4 py-2 font-medium">Status</th>
                  <th class="px-4 py-2 font-medium"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                @for (s of staff(); track s.id) {
                  <tr>
                    <td class="px-4 py-2 font-medium text-slate-800">{{ s.fullName }}</td>
                    <td class="px-4 py-2 text-slate-500">{{ s.email }}</td>
                    <td class="px-4 py-2">{{ s.staffType }}</td>
                    <td class="px-4 py-2">
                      <span class="rounded-full px-2 py-0.5 text-xs font-semibold"
                        [class.bg-emerald-100]="!s.suspended" [class.text-emerald-700]="!s.suspended"
                        [class.bg-rose-100]="s.suspended" [class.text-rose-700]="s.suspended">
                        {{ s.suspended ? 'Suspended' : 'Active' }}
                      </span>
                    </td>
                    <td class="px-4 py-2 text-right">
                      @if (s.suspended) {
                        <button type="button" [disabled]="busy() === s.id" (click)="reactivate(s)" class="text-sm font-medium text-emerald-600 hover:text-emerald-700">Reactivate</button>
                      } @else {
                        <button type="button" [disabled]="busy() === s.id" (click)="suspend(s)" class="text-sm font-medium text-rose-600 hover:text-rose-700">Suspend</button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <p class="mt-2 text-xs text-slate-400">{{ total() }} staff member(s)</p>
        }
      </section>

      <section class="rounded-xl border border-slate-200 bg-white p-4">
        <h2 class="mb-3 font-semibold text-slate-900">Add staff</h2>
        <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3">
          <div>
            <label for="fullName" class="mb-1 block text-sm font-medium text-slate-700">Full name</label>
            <input id="fullName" type="text" formControlName="fullName" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
          </div>
          <div>
            <label for="email" class="mb-1 block text-sm font-medium text-slate-700">Email</label>
            <input id="email" type="email" formControlName="email" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
          </div>
          <div>
            <label for="password" class="mb-1 block text-sm font-medium text-slate-700">Temporary password</label>
            <input id="password" type="password" formControlName="password" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
            <p class="mt-1 text-xs text-slate-400">≥10 chars, with upper, lower and a digit.</p>
          </div>
          <div>
            <label for="staffType" class="mb-1 block text-sm font-medium text-slate-700">Role</label>
            <select id="staffType" formControlName="staffType" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500">
              @for (t of staffTypes; track t) {
                <option [value]="t">{{ t }}</option>
              }
            </select>
          </div>
          <button type="submit" [disabled]="submitting()" class="w-full rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-50">
            {{ submitting() ? 'Creating…' : 'Add staff' }}
          </button>
        </form>
      </section>
    </div>
  `,
})
export class VendorStaffComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(VendorApiService);
  private readonly toasts = inject(ToastService);

  protected readonly staffTypes = STAFF_TYPES;
  protected readonly staff = signal<Staff[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly busy = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.pattern(STRONG_PASSWORD)]],
    staffType: ['Employee' as StaffType, [Validators.required]],
  });

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.listStaff().subscribe({
      next: (page) => {
        this.staff.set(page.items);
        this.total.set(page.total);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toasts.info('Please complete the form — the password must meet the policy.');
      return;
    }
    const v = this.form.getRawValue();
    const body: CreateStaffRequest = {
      fullName: v.fullName.trim(),
      email: v.email.trim().toLowerCase(),
      password: v.password,
      staffType: v.staffType,
    };
    this.submitting.set(true);
    this.api.createStaff(body).subscribe({
      next: () => {
        this.submitting.set(false);
        this.toasts.success(`Staff account created for ${body.email}.`);
        this.form.reset({ fullName: '', email: '', password: '', staffType: 'Employee' });
        this.load();
      },
      error: () => this.submitting.set(false),
    });
  }

  protected suspend(s: Staff): void {
    this.busy.set(s.id);
    this.api.suspendStaff(s.id).subscribe({
      next: () => { this.busy.set(null); this.toasts.success(`${s.fullName} suspended.`); this.load(); },
      error: () => this.busy.set(null),
    });
  }

  protected reactivate(s: Staff): void {
    this.busy.set(s.id);
    this.api.reactivateStaff(s.id).subscribe({
      next: () => { this.busy.set(null); this.toasts.success(`${s.fullName} reactivated.`); this.load(); },
      error: () => this.busy.set(null),
    });
  }
}
