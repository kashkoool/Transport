import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorBell, phosphorBellSimple, phosphorCheck, phosphorX } from '@ng-icons/phosphor-icons/regular';
import { NotificationService } from './notification.service';

/** Header bell: unread badge + a dropdown of recent notifications. */
@Component({
  selector: 'app-notification-bell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgIcon],
  providers: [provideIcons({ phosphorBell, phosphorBellSimple, phosphorCheck, phosphorX })],
  template: `
    <div class="relative">
      <button
        type="button"
        (click)="toggle()"
        class="navlink relative cursor-pointer px-2.5"
        aria-label="Notifications"
        [attr.aria-expanded]="open()"
      >
        <ng-icon name="phosphorBell" class="text-lg" />
        @if (notifications.unreadCount() > 0) {
          <span
            class="absolute -end-1 -top-1 inline-flex min-w-4 items-center justify-center rounded-full bg-rose-600 px-1 text-[10px] font-semibold text-white ring-2 ring-white dark:ring-ink-800"
            >{{ notifications.unreadCount() }}</span
          >
        }
      </button>

      @if (open()) {
        <div
          class="absolute end-0 z-20 mt-2 w-80 overflow-hidden rounded-2xl bg-white ring-1 ring-slate-200/70 dark:bg-ink-800 dark:ring-white/10"
          style="box-shadow: var(--shadow-card-hover)"
        >
          <div class="flex items-center justify-between border-b border-slate-100 px-4 py-3 dark:border-white/10">
            <span class="text-sm font-bold text-slate-800 dark:text-slate-100">Notifications</span>
            <button
              type="button"
              (click)="notifications.markAllRead()"
              class="inline-flex cursor-pointer items-center gap-1 text-xs font-semibold text-brand-600 transition hover:text-brand-700 dark:text-brand-400 dark:hover:text-brand-300"
            >
              <ng-icon name="phosphorCheck" class="text-sm" /> Mark all read
            </button>
          </div>
          <div class="stagger-children max-h-80 overflow-y-auto">
            @for (n of notifications.notifications(); track n.id) {
              <div
                class="flex items-start gap-2 border-b border-slate-50 px-4 py-3 transition hover:bg-slate-50 dark:border-white/5 dark:hover:bg-white/5"
                [class]="!n.isRead ? 'bg-brand-50 dark:bg-brand-500/10' : ''"
              >
                <button type="button" (click)="notifications.markRead(n.id)" class="flex-1 cursor-pointer text-start">
                  <p class="text-sm font-semibold text-slate-800 dark:text-slate-100">{{ n.title }}</p>
                  <p class="text-xs text-slate-500 dark:text-slate-400">{{ n.message }}</p>
                </button>
                <button
                  type="button"
                  (click)="notifications.delete(n.id)"
                  class="shrink-0 cursor-pointer rounded-full p-1 text-slate-400 transition hover:bg-rose-50 hover:text-rose-600 dark:hover:bg-rose-500/10"
                  aria-label="Delete notification"
                >
                  <ng-icon name="phosphorX" class="text-sm" />
                </button>
              </div>
            } @empty {
              <div class="flex flex-col items-center gap-2 px-4 py-10 text-center">
                <ng-icon name="phosphorBellSimple" class="text-3xl text-slate-300 dark:text-slate-600" />
                <p class="text-sm text-slate-400 dark:text-slate-500">No notifications yet</p>
              </div>
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
