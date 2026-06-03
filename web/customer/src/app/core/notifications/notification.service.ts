import { Injectable, effect, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';

export interface AppNotification {
  id: string;
  title: string;
  message: string;
  type: string;
  isRead: boolean;
  createdAtUtc: string;
}

/**
 * In-app notifications: loads the inbox over REST and subscribes to live pushes over SignalR.
 * Connects/disconnects automatically with the auth session; SignalR failure degrades gracefully
 * to REST (the inbox still loads, just without live updates).
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly api = `${environment.apiBaseUrl}/notifications`;

  readonly notifications = signal<AppNotification[]>([]);
  readonly unreadCount = signal(0);

  private connection: signalR.HubConnection | null = null;

  constructor() {
    effect(() => {
      if (this.auth.isAuthenticated()) {
        this.start();
      } else {
        this.stop();
      }
    });
  }

  refresh(): void {
    this.http
      .get<{ data: AppNotification[] }>(`${this.api}?limit=20`)
      .subscribe((page) => this.notifications.set(page.data));
    this.http
      .get<{ count: number }>(`${this.api}/unread-count`)
      .subscribe((r) => this.unreadCount.set(r.count));
  }

  markRead(id: string): void {
    this.http.post(`${this.api}/${id}/read`, {}).subscribe(() => {
      this.notifications.update((list) => list.map((n) => (n.id === id ? { ...n, isRead: true } : n)));
      this.unreadCount.update((c) => Math.max(0, c - 1));
    });
  }

  markAllRead(): void {
    this.http.post(`${this.api}/read-all`, {}).subscribe(() => {
      this.notifications.update((list) => list.map((n) => ({ ...n, isRead: true })));
      this.unreadCount.set(0);
    });
  }

  private start(): void {
    this.refresh();
    if (this.connection) return;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/realtime', { accessTokenFactory: () => this.auth.token() ?? '' })
      .withAutomaticReconnect()
      .build();

    this.connection.on('notification', (n: AppNotification) => {
      this.notifications.update((list) => [n, ...list]);
      this.unreadCount.update((c) => c + 1);
    });

    // Best-effort: if the socket can't connect, the REST inbox above still works.
    this.connection.start().catch(() => undefined);
  }

  private stop(): void {
    this.notifications.set([]);
    this.unreadCount.set(0);
    this.connection?.stop().catch(() => undefined);
    this.connection = null;
  }
}
