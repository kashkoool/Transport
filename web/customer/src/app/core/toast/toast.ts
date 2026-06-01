import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from './toast.service';

/** Fixed-position stack of dismissible toasts, driven by ToastService. */
@Component({
  selector: 'app-toast',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="fixed bottom-4 right-4 z-50 flex w-80 flex-col gap-2">
      @for (toast of toasts.toasts(); track toast.id) {
        <div
          class="flex items-start gap-3 rounded-lg px-4 py-3 text-sm shadow-lg ring-1 ring-black/5"
          [class.bg-emerald-600]="toast.kind === 'success'"
          [class.bg-rose-600]="toast.kind === 'error'"
          [class.bg-slate-800]="toast.kind === 'info'"
        >
          <span class="flex-1 text-white">{{ toast.message }}</span>
          <button
            type="button"
            class="text-white/70 hover:text-white"
            (click)="toasts.dismiss(toast.id)"
            aria-label="Dismiss"
          >
            ✕
          </button>
        </div>
      }
    </div>
  `,
})
export class ToastComponent {
  protected readonly toasts = inject(ToastService);
}
