import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AddDriverRequest, VendorApiService } from '../../core/api/vendor-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { Driver } from '../../core/models';
import { VendorNavComponent } from './vendor-nav';

@Component({
  selector: 'app-vendor-drivers',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, VendorNavComponent],
  template: `
    <app-vendor-nav />
    <h1 class="mb-6 text-2xl font-bold text-slate-900">Drivers</h1>

    <div class="grid gap-6 lg:grid-cols-3">
      <section class="lg:col-span-2">
        @if (loading()) {
          <p class="text-slate-500">Loading…</p>
        } @else if (drivers().length === 0) {
          <p class="rounded-lg bg-slate-100 p-4 text-slate-600">No drivers yet. Add one, then assign them to a bus in Fleet.</p>
        } @else {
          <table class="w-full overflow-hidden rounded-xl border border-slate-200 bg-white text-sm">
            <thead class="bg-slate-50 text-left text-slate-500">
              <tr>
                <th class="px-4 py-2 font-medium">Name</th>
                <th class="px-4 py-2 font-medium">Phone</th>
                <th class="px-4 py-2 font-medium">License #</th>
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
          <p class="mt-2 text-xs text-slate-400">{{ total() }} driver(s)</p>
        }
      </section>

      <section class="rounded-xl border border-slate-200 bg-white p-4">
        <h2 class="mb-3 font-semibold text-slate-900">Add a driver</h2>
        <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3">
          <div>
            <label for="fullName" class="mb-1 block text-sm font-medium text-slate-700">Full name</label>
            <input id="fullName" type="text" formControlName="fullName" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
          </div>
          <div>
            <label for="phone" class="mb-1 block text-sm font-medium text-slate-700">Phone (optional)</label>
            <input id="phone" type="tel" formControlName="phone" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
          </div>
          <div>
            <label for="licenseNumber" class="mb-1 block text-sm font-medium text-slate-700">License # (optional)</label>
            <input id="licenseNumber" type="text" formControlName="licenseNumber" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
          </div>
          <button type="submit" [disabled]="submitting()" class="w-full rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-50">
            {{ submitting() ? 'Adding…' : 'Add driver' }}
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

  protected readonly drivers = signal<Driver[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(100)]],
    phone: ['', [Validators.maxLength(30)]],
    licenseNumber: ['', [Validators.maxLength(40)]],
  });

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.listDrivers().subscribe({
      next: (page) => {
        this.drivers.set(page.items);
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
        this.toasts.success(`Driver ${body.fullName} added.`);
        this.form.reset({ fullName: '', phone: '', licenseNumber: '' });
        this.load();
      },
      error: () => this.submitting.set(false),
    });
  }
}
