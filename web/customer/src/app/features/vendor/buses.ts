import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AddBusRequest, VendorApiService } from '../../core/api/vendor-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { Bus, BusType } from '../../core/models';
import { VendorNavComponent } from './vendor-nav';

const BUS_TYPES: BusType[] = ['Standard', 'Premium', 'Luxury', 'Sleeper'];

@Component({
  selector: 'app-vendor-buses',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, VendorNavComponent],
  template: `
    <app-vendor-nav />
    <h1 class="mb-6 text-2xl font-bold text-slate-900">Fleet</h1>

    <div class="grid gap-6 lg:grid-cols-3">
      <section class="lg:col-span-2">
        @if (loading()) {
          <p class="text-slate-500">Loading…</p>
        } @else if (buses().length === 0) {
          <p class="rounded-lg bg-slate-100 p-4 text-slate-600">No buses yet. Add your first one →</p>
        } @else {
          <table class="w-full overflow-hidden rounded-xl border border-slate-200 bg-white text-sm">
            <thead class="bg-slate-50 text-left text-slate-500">
              <tr>
                <th class="px-4 py-2 font-medium">Bus #</th>
                <th class="px-4 py-2 font-medium">Seats</th>
                <th class="px-4 py-2 font-medium">Type</th>
                <th class="px-4 py-2 font-medium">Model</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100">
              @for (b of buses(); track b.id) {
                <tr>
                  <td class="px-4 py-2 font-medium text-slate-800">{{ b.busNumber }}</td>
                  <td class="px-4 py-2">{{ b.seatCount }}</td>
                  <td class="px-4 py-2">{{ b.type }}</td>
                  <td class="px-4 py-2 text-slate-500">{{ b.model || '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
          <p class="mt-2 text-xs text-slate-400">{{ total() }} bus(es)</p>
        }
      </section>

      <section class="rounded-xl border border-slate-200 bg-white p-4">
        <h2 class="mb-3 font-semibold text-slate-900">Add a bus</h2>
        <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3">
          <div>
            <label for="busNumber" class="mb-1 block text-sm font-medium text-slate-700">Bus number</label>
            <input id="busNumber" type="text" formControlName="busNumber" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
          </div>
          <div>
            <label for="seatCount" class="mb-1 block text-sm font-medium text-slate-700">Seats</label>
            <input id="seatCount" type="number" min="1" max="120" formControlName="seatCount" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
          </div>
          <div>
            <label for="type" class="mb-1 block text-sm font-medium text-slate-700">Type</label>
            <select id="type" formControlName="type" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500">
              @for (t of busTypes; track t) {
                <option [value]="t">{{ t }}</option>
              }
            </select>
          </div>
          <div>
            <label for="model" class="mb-1 block text-sm font-medium text-slate-700">Model (optional)</label>
            <input id="model" type="text" formControlName="model" class="w-full rounded-md border border-slate-300 px-3 py-2 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" />
          </div>
          <button type="submit" [disabled]="submitting()" class="w-full rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-50">
            {{ submitting() ? 'Adding…' : 'Add bus' }}
          </button>
        </form>
      </section>
    </div>
  `,
})
export class VendorBusesComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(VendorApiService);
  private readonly toasts = inject(ToastService);

  protected readonly busTypes = BUS_TYPES;
  protected readonly buses = signal<Bus[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    busNumber: ['', [Validators.required, Validators.maxLength(40)]],
    seatCount: [40, [Validators.required, Validators.min(1), Validators.max(120)]],
    type: ['Standard' as BusType, [Validators.required]],
    model: ['', [Validators.maxLength(100)]],
  });

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.listBuses().subscribe({
      next: (page) => {
        this.buses.set(page.items);
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
    const body: AddBusRequest = {
      busNumber: v.busNumber.trim(),
      seatCount: v.seatCount,
      type: v.type,
      model: v.model.trim() || null,
    };
    this.submitting.set(true);
    this.api.addBus(body).subscribe({
      next: () => {
        this.submitting.set(false);
        this.toasts.success(`Bus ${body.busNumber} added.`);
        this.form.reset({ busNumber: '', seatCount: 40, type: 'Standard', model: '' });
        this.load();
      },
      error: () => this.submitting.set(false),
    });
  }
}
