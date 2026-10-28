import { Injectable, inject, signal } from '@angular/core';
import { Subscription, forkJoin } from 'rxjs';
import { LIVE_CONNECTION, LiveConnection } from '../api/live-connection';
import { NotificationApi } from '../api/notification.api';
import { AppNotification } from '../models/notification.models';

const POLL_MS = 30_000;
const RECONNECT_MS = 30_000;
const RECENT_COUNT = 8;

/**
 * Holds the signed-in user's unread count and newest notifications. New ones arrive live over a connection. When
 * the connection is down the app polls instead, and keeps trying to reconnect. Start it when someone signs in and
 * stop it when they sign out.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly api = inject(NotificationApi);
  private readonly createConnection = inject(LIVE_CONNECTION);

  private readonly unreadState = signal(0);
  private readonly recentState = signal<AppNotification[]>([]);
  private readonly liveState = signal(false);
  private readonly announcementState = signal('');
  private readonly versionState = signal(0);

  readonly unread = this.unreadState.asReadonly();
  readonly recent = this.recentState.asReadonly();
  readonly live = this.liveState.asReadonly();

  /** Text for a screen reader to announce when something new arrives. */
  readonly announcement = this.announcementState.asReadonly();

  /** Changes whenever the stored notifications may have changed, so a page showing them can reload. */
  readonly version = this.versionState.asReadonly();

  private running = false;
  private connection: LiveConnection | null = null;
  private connectedBefore = false;
  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  private refreshing: Subscription | null = null;

  start(): void {
    if (this.running) {
      return;
    }

    this.running = true;
    this.refresh();
    this.connect();
    this.pollTimer = setInterval(() => {
      if (!this.liveState()) {
        this.refresh();
      }
    }, POLL_MS);
  }

  stop(): void {
    this.running = false;

    if (this.pollTimer !== null) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }

    if (this.retryTimer !== null) {
      clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }

    this.refreshing?.unsubscribe();
    this.refreshing = null;

    const connection = this.connection;
    this.connection = null;
    this.connectedBefore = false;

    if (connection) {
      void connection.stop().catch(() => undefined);
    }

    this.unreadState.set(0);
    this.recentState.set([]);
    this.liveState.set(false);
    this.announcementState.set('');
  }

  /** Loads the count and the newest notifications. A failure keeps what is shown and tries again later. */
  refresh(): void {
    this.refreshing?.unsubscribe();
    this.refreshing = forkJoin({ count: this.api.unreadCount(), page: this.api.list({}, RECENT_COUNT) }).subscribe({
      next: ({ count, page }) => {
        this.unreadState.set(count);
        this.recentState.set(page.items);
      },
      error: () => undefined,
    });
  }

  markRead(notification: AppNotification): void {
    const stored = this.recentState().find((item) => item.id === notification.id);

    if (notification.isRead || stored?.isRead) {
      return;
    }

    this.recentState.update((items) => items.map((item) => (item.id === notification.id ? { ...item, isRead: true } : item)));
    this.unreadState.update((count) => Math.max(0, count - 1));

    this.api.markRead(notification.id).subscribe({
      next: () => this.versionState.update((version) => version + 1),
      error: () => this.refresh(), // put the real state back
    });
  }

  markAllRead(): void {
    this.recentState.update((items) => items.map((item) => ({ ...item, isRead: true })));
    this.unreadState.set(0);

    this.api.markAllRead().subscribe({
      next: () => this.versionState.update((version) => version + 1),
      error: () => this.refresh(),
    });
  }

  private connect(): void {
    const connection = this.createConnection();
    this.connection = connection;

    // Events from a connection that has been replaced or stopped are ignored.
    const current = (): boolean => this.running && this.connection === connection;

    connection.onNotification((notification) => {
      if (current()) {
        this.receive(notification);
      }
    });

    connection.onClose(() => {
      if (current()) {
        this.liveState.set(false);
        this.scheduleReconnect();
      }
    });

    connection.onReconnected(() => {
      if (current()) {
        this.liveState.set(true);
        this.refresh(); // anything that arrived while the connection was down
      }
    });

    connection.start().then(
      () => {
        if (!current()) {
          return;
        }

        this.liveState.set(true);

        if (this.connectedBefore) {
          this.refresh();
        }

        this.connectedBefore = true;
      },
      () => {
        if (current()) {
          this.liveState.set(false);
          this.scheduleReconnect();
        }
      },
    );
  }

  private scheduleReconnect(): void {
    if (!this.running || this.retryTimer !== null) {
      return;
    }

    this.retryTimer = setTimeout(() => {
      this.retryTimer = null;

      if (this.running) {
        const old = this.connection;
        this.connection = null;

        if (old) {
          void old.stop().catch(() => undefined);
        }

        this.connect();
      }
    }, RECONNECT_MS);
  }

  private receive(notification: AppNotification): void {
    if (this.recentState().some((item) => item.id === notification.id)) {
      return; // a refresh already brought it in
    }

    this.recentState.update((items) => [notification, ...items].slice(0, RECENT_COUNT));

    if (!notification.isRead) {
      this.unreadState.update((count) => count + 1);
    }

    this.announcementState.set(`New notification: ${notification.title}`);
    this.versionState.update((version) => version + 1);
  }
}