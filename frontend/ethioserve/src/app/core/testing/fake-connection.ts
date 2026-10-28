import { LiveConnection } from '../api/live-connection';
import { AppNotification } from '../models/notification.models';

export class FakeConnection implements LiveConnection {
  starts = 0;
  stops = 0;
  startError: Error | null = null;

  private notificationHandlers: ((notification: AppNotification) => void)[] = [];
  private closeHandlers: (() => void)[] = [];
  private reconnectedHandlers: (() => void)[] = [];

  start(): Promise<void> {
    this.starts++;

    return this.startError ? Promise.reject(this.startError) : Promise.resolve();
  }

  stop(): Promise<void> {
    this.stops++;

    return Promise.resolve();
  }

  onNotification(handler: (notification: AppNotification) => void): void {
    this.notificationHandlers.push(handler);
  }

  onClose(handler: () => void): void {
    this.closeHandlers.push(handler);
  }

  onReconnected(handler: () => void): void {
    this.reconnectedHandlers.push(handler);
  }

  /** The server pushes a notification. */
  emit(notification: AppNotification): void {
    this.notificationHandlers.forEach((handler) => handler(notification));
  }

  /** The connection drops for good. */
  close(): void {
    this.closeHandlers.forEach((handler) => handler());
  }

  /** The connection comes back by itself. */
  reconnected(): void {
    this.reconnectedHandlers.forEach((handler) => handler());
  }
}

export function notification(id: string, overrides: Partial<AppNotification> = {}): AppNotification {
  return {
    id,
    type: 'General',
    title: `Title ${id}`,
    message: `Message ${id}`,
    isRead: false,
    relatedEntityType: null,
    relatedEntityId: null,
    createdAt: new Date().toISOString(),
    ...overrides,
  };
}