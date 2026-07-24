import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  phosphorBus,
  phosphorMagnifyingGlass,
  phosphorPencilSimple,
  phosphorTrash,
  phosphorCheck,
  phosphorPlus,
  phosphorFloppyDisk,
} from '@ng-icons/phosphor-icons/regular';
import {
  AddBusRequest,
  UpdateBusRequest,
  VendorApiService,
} from '../../core/api/vendor-api.service';
import { ToastService } from '../../core/toast/toast.service';
import { Bus, BusType, Driver } from '../../core/models';
import { VendorNavComponent } from './vendor-nav';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

const BUS_TYPES: BusType[] = ['Standard', 'Premium', 'Luxury', 'Sleeper'];
// Map each API bus-type value to its dictionary key so the <option> label can be translated
// while the underlying [value] stays the raw enum the API expects.
const BUS_TYPE_KEY: Record<BusType, string> = {
  Standard: 'vendor.buses.type.standard',
  Premium: 'vendor.buses.type.premium',
  Luxury: 'vendor.buses.type.luxury',
  Sleeper: 'vendor.buses.type.sleeper',
};

@Component({
  selector: 'app-vendor-buses',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, VendorNavComponent, NgIcon, TranslatePipe],
  providers: [
    provideIcons({
      phosphorBus,
      phosphorMagnifyingGlass,
      phosphorPencilSimple,
      phosphorTrash,
      phosphorCheck,
      phosphorPlus,
      phosphorFloppyDisk,
    }),
  ],
  template: `
    <div class="lg:grid lg:grid-cols-[15rem_1fr] lg:items-start lg:gap-8">
      <app-vendor-nav />

      <div class="min-w-0">
        <h1 class="animate-in mb-6 font-display text-2xl font-bold text-ink dark:text-white">{{ 'vendor.buses.title' | t }}</h1>

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
                  [placeholder]="'vendor.buses.searchPlaceholder' | t"
                  class="input ps-10"
                />
              </div>
              <p class="text-sm text-slate-500 dark:text-slate-400">{{ 'vendor.buses.count' | t: { n: total() } }}</p>
            </div>

            @if (loading()) {
              <p class="text-slate-500 dark:text-slate-400">{{ 'vendor.common.loading' | t }}</p>
            } @else if (buses().length === 0) {
              <div class="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-slate-200 bg-slate-50/60 px-6 py-14 text-center dark:border-white/10 dark:bg-white/2">
                <span class="grid h-12 w-12 place-items-center rounded-full bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300">
                  <ng-icon name="phosphorBus" class="text-2xl" aria-hidden="true" />
                </span>
                <p class="text-sm font-medium text-slate-600 dark:text-slate-300">{{ 'vendor.buses.empty' | t }}</p>
                <a href="#bus-form-panel" class="btn btn-primary px-4 py-2 text-sm">
                  <ng-icon name="phosphorPlus" aria-hidden="true" />
                  {{ 'vendor.buses.addBus' | t }}
                </a>
              </div>
            } @else {
              <div class="card overflow-hidden">
                <div class="overflow-x-auto">
                  <table class="w-full text-sm">
                    <thead class="bg-slate-50 text-start text-xs font-semibold tracking-wide text-slate-500 uppercase dark:bg-white/5 dark:text-slate-400">
                      <tr>
                        <th class="px-4 py-3 text-start font-semibold">{{ 'vendor.buses.th.busNumber' | t }}</th>
                        <th class="px-4 py-3 text-end font-semibold">{{ 'vendor.buses.th.seats' | t }}</th>
                        <th class="px-4 py-3 text-end font-semibold">{{ 'vendor.buses.th.perRow' | t }}</th>
                        <th class="px-4 py-3 text-start font-semibold">{{ 'vendor.buses.th.type' | t }}</th>
                        <th class="px-4 py-3 text-start font-semibold">{{ 'vendor.buses.th.driver' | t }}</th>
                        <th class="px-4 py-3"></th>
                      </tr>
                    </thead>
                    <tbody class="stagger-children divide-y divide-slate-100 dark:divide-white/10">
                      @for (b of buses(); track b.id) {
                        <tr class="transition-colors hover:bg-slate-50 dark:hover:bg-white/3">
                          <td class="px-4 py-3 font-medium text-slate-800 dark:text-slate-100">{{ b.busNumber }}</td>
                          <td class="px-4 py-3 text-end tabular-nums text-slate-600 dark:text-slate-300">{{ b.seatCount }}</td>
                          <td class="px-4 py-3 text-end tabular-nums text-slate-600 dark:text-slate-300">{{ b.seatsPerRow }}</td>
                          <td class="px-4 py-3 text-slate-600 dark:text-slate-300">{{ typeKey(b.type) | t }}</td>
                          <td class="px-4 py-3">
                            <select
                              [value]="b.driverId ?? ''"
                              (change)="assignDriver(b, $any($event.target).value)"
                              class="rounded-lg border-0 bg-slate-100 px-2 py-1.5 text-sm ring-1 ring-slate-200 ring-inset focus:ring-2 focus:ring-brand-500 focus:outline-none dark:bg-white/5 dark:text-slate-100 dark:ring-white/10"
                            >
                              <option value="">{{ 'vendor.common.none' | t }}</option>
                              @for (d of drivers(); track d.id) {
                                <option [value]="d.id">{{ d.fullName }}</option>
                              }
                            </select>
                          </td>
                          <td class="px-4 py-3 text-end whitespace-nowrap">
                            <button type="button" (click)="edit(b)" [attr.aria-label]="'vendor.common.edit' | t" class="btn btn-ghost px-2.5 py-1.5 text-xs">
                              <ng-icon name="phosphorPencilSimple" aria-hidden="true" />
                            </button>
                            @if (confirmingDelete() === b.id) {
                              <button type="button" [disabled]="busy()" (click)="remove(b)" class="btn btn-danger ms-1 px-2.5 py-1.5 text-xs">
                                <ng-icon name="phosphorCheck" aria-hidden="true" />{{ 'vendor.common.confirm' | t }}
                              </button>
                              <button type="button" (click)="confirmingDelete.set(null)" class="btn btn-ghost ms-1 px-2.5 py-1.5 text-xs">{{ 'vendor.common.keep' | t }}</button>
                            } @else {
                              <button type="button" (click)="confirmingDelete.set(b.id)" [attr.aria-label]="'vendor.common.delete' | t" class="btn btn-ghost ms-1 px-2.5 py-1.5 text-xs text-slate-400 hover:text-rose-600 dark:text-slate-500">
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

          <section id="bus-form-panel" class="card p-5">
            <h2 class="mb-3 font-display font-semibold text-ink dark:text-white">{{ (editingId() ? 'vendor.buses.editBus' : 'vendor.buses.addBus') | t }}</h2>
            <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3.5">
              <div>
                <label for="busNumber" class="label">{{ 'vendor.buses.busNumber' | t }}</label>
                <input id="busNumber" type="text" formControlName="busNumber" [readonly]="!!editingId()" class="input read-only:bg-slate-50 dark:read-only:bg-white/3" />
              </div>
              <div class="grid grid-cols-2 gap-2.5">
                <div>
                  <label for="seatCount" class="label">{{ 'vendor.buses.seats' | t }}</label>
                  <input id="seatCount" type="number" min="1" max="120" formControlName="seatCount" class="input" />
                </div>
                <div>
                  <label for="seatsPerRow" class="label">{{ 'vendor.buses.seatsPerRow' | t }}</label>
                  <input id="seatsPerRow" type="number" min="1" max="6" formControlName="seatsPerRow" class="input" />
                </div>
              </div>
              <div>
                <label for="type" class="label">{{ 'vendor.buses.type' | t }}</label>
                <select id="type" formControlName="type" class="input">
                  @for (t of busTypes; track t) {
                    <option [value]="t">{{ typeKey(t) | t }}</option>
                  }
                </select>
              </div>
              <div>
                <label for="model" class="label">{{ 'vendor.buses.model' | t }}</label>
                <input id="model" type="text" formControlName="model" class="input" />
              </div>
              <button type="submit" [disabled]="submitting()" class="btn btn-primary w-full">
                <ng-icon [name]="editingId() ? 'phosphorFloppyDisk' : 'phosphorPlus'" aria-hidden="true" />
                {{ (submitting() ? 'vendor.common.saving' : editingId() ? 'vendor.common.saveChanges' : 'vendor.buses.addBusBtn') | t }}
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
export class VendorBusesComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(VendorApiService);
  private readonly toasts = inject(ToastService);
  private readonly i18n = inject(TranslationService);

  protected readonly busTypes = BUS_TYPES;
  /** Dictionary key for a bus type, so the template can translate the label. */
  protected typeKey(type: BusType): string {
    return BUS_TYPE_KEY[type] ?? type;
  }
  protected readonly buses = signal<Bus[]>([]);
  protected readonly drivers = signal<Driver[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly confirmingDelete = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly search = signal('');
  private searchTimer?: ReturnType<typeof setTimeout>;

  protected readonly form = this.fb.nonNullable.group({
    busNumber: ['', [Validators.required, Validators.maxLength(40)]],
    seatCount: [40, [Validators.required, Validators.min(1), Validators.max(120)]],
    seatsPerRow: [4, [Validators.required, Validators.min(1), Validators.max(6)]],
    type: ['Standard' as BusType, [Validators.required]],
    model: ['', [Validators.maxLength(100)]],
  });

  ngOnInit(): void {
    this.load();
    this.api.listDrivers().subscribe({ next: (p) => this.drivers.set(p.data) });
  }

  protected onSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.load(), 250); // debounce
  }

  private load(): void {
    this.loading.set(true);
    this.api.listBuses(1, 100, this.search().trim() || undefined).subscribe({
      next: (page) => {
        this.buses.set(page.data);
        this.total.set(page.total);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected edit(bus: Bus): void {
    this.editingId.set(bus.id);
    this.form.reset({
      busNumber: bus.busNumber,
      seatCount: bus.seatCount,
      seatsPerRow: bus.seatsPerRow,
      type: bus.type,
      model: bus.model ?? '',
    });
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
    this.form.reset({ busNumber: '', seatCount: 40, seatsPerRow: 4, type: 'Standard', model: '' });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.submitting.set(true);
    const editId = this.editingId();
    if (editId) {
      const body: UpdateBusRequest = {
        seatCount: v.seatCount,
        seatsPerRow: v.seatsPerRow,
        type: v.type,
        model: v.model.trim() || null,
      };
      this.api.updateBus(editId, body).subscribe({
        next: () => {
          this.submitting.set(false);
          this.toasts.success(this.i18n.t('vendor.buses.toast.updated'));
          this.cancelEdit();
          this.load();
        },
        error: () => this.submitting.set(false),
      });
    } else {
      const body: AddBusRequest = {
        busNumber: v.busNumber.trim(),
        seatCount: v.seatCount,
        seatsPerRow: v.seatsPerRow,
        type: v.type,
        model: v.model.trim() || null,
      };
      this.api.addBus(body).subscribe({
        next: () => {
          this.submitting.set(false);
          this.toasts.success(this.i18n.t('vendor.buses.toast.added', { number: body.busNumber }));
          this.cancelEdit();
          this.load();
        },
        error: () => this.submitting.set(false),
      });
    }
  }

  protected remove(bus: Bus): void {
    this.busy.set(true);
    this.api.deleteBus(bus.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.confirmingDelete.set(null);
        this.toasts.success(this.i18n.t('vendor.buses.toast.deleted', { number: bus.busNumber }));
        this.load();
      },
      error: () => {
        this.busy.set(false);
        this.confirmingDelete.set(null);
      },
    });
  }

  protected assignDriver(bus: Bus, driverId: string): void {
    this.api.assignDriver(bus.id, driverId || null).subscribe({
      next: () => this.toasts.success(this.i18n.t('vendor.buses.toast.driverUpdated')),
      error: () => this.load(), // revert the select to the server's truth on failure
    });
  }
}
