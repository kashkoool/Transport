import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NotificationService } from './notification.service';

/** Header bell: unread badge + a dropdown of recent notifications. */
@Component({
  selector: 'app-notification-bell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="relative">
      <button
        type="button"
        (click)="toggle()"
        class="relative rounded-md px-2 py-1 text-lg hover:bg-slate-100"
        aria-label="Notifications"
      >
        🔔
        @if (notifications.unreadCount() > 0) {
          <span
            class="absolute -right-1 -top-1 inline-flex min-w-4 items-center justify-center rounded-full bg-red-600 px-1 text-[10px] font-semibold text-white"
            >{{ notifications.unreadCount() }}</span
          >
        }
      </button>

      @if (open()) {
        <div class="absolute right-0 z-20 mt-2 w-80 rounded-md border border-slate-200 bg-white shadow-lg">
          <div class="flex items-center justify-between border-b border-slate-100 px-3 py-2">
            <span class="text-sm font-semibold text-slate-700">Notifications</span>
            <button type="button" (click)="notifications.markAllRead()" class="text-xs text-brand-600 hover:text-brand-700">
              Mark all read
            </button>
          </div>
          <div class="max-h-80 overflow-y-auto">
            @for (n of notifications.notifications(); track n.id) {
              <div
                class="flex items-start gap-2 border-b border-slate-50 px-3 py-2 hover:bg-slate-50"
                [class.bg-brand-50]="!n.isRead"
              >
                <button type="button" (click)="notifications.markRead(n.id)" class="flex-1 text-left">
                  <p class="text-sm font-medium text-slate-800">{{ n.title }}</p>
                  <p class="text-xs text-slate-500">{{ n.message }}</p>
                </button>
                <button
                  type="button"
                  (click)="notifications.delete(n.id)"
                  class="shrink-0 text-slate-400 hover:text-rose-600"
                  aria-label="Delete notification"
                >✕</button>
              </div>
            } @empty {
              <p class="px-3 py-6 text-center text-sm text-slate-400">No notifications</p>
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class NotificationBellComponent {
  protected readonly notifications = inject(NotificationService);
  protected readonly open = signal(false);

  protected toggle(): void {
    this.open.update((o) => !o);
  }
}
