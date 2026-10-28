import { InjectionToken, inject } from '@angular/core';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { AuthService } from '../auth/auth.service';
import { AppNotification } from '../models/notification.models';

/** The part of a live connection the app needs. Tests replace it with a fake, so no network is involved. */
export interface LiveConnection {
  start(): Promise<void>;
  stop(): Promise<void>;
  onNotification(handler: (notification: AppNotification) => void): void;
  onClose(handler: () => void): void;
  onReconnected(handler: () => void): void;
}

export const HUB_URL = '/hubs/notifications';

export function createSignalRConnection(accessToken: () => string): LiveConnection {
  const connection = new HubConnectionBuilder()
    .withUrl(HUB_URL, { accessTokenFactory: accessToken })
    .withAutomaticReconnect([0, 2000, 10_000, 30_000])
    .configureLogging(LogLevel.Warning)
    .build();

  return {
    start: () => connection.start(),
    stop: () => connection.stop(),
    onNotification: (handler) => connection.on('notification', handler),
    onClose: (handler) => connection.onclose(() => handler()),
    onReconnected: (handler) => connection.onreconnected(() => handler()),
  };
}

/** A function that makes a fresh connection each time the app needs one. */
export const LIVE_CONNECTION = new InjectionToken<() => LiveConnection>('LIVE_CONNECTION', {
  providedIn: 'root',
  factory: () => {
    const auth = inject(AuthService);

    return () => createSignalRConnection(() => auth.accessToken() ?? '');
  },
});