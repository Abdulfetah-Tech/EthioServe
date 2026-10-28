import { NotificationType } from '../core/models/notification.models';

const MINUTE = 60_000;

/** For example "5 min ago". A clock that is slightly ahead of the server reads as "Just now". */
export function timeAgo(iso: string, now: number = Date.now()): string {
  const diff = now - Date.parse(iso);

  if (Number.isNaN(diff)) {
    return '';
  }

  if (diff < MINUTE) {
    return 'Just now';
  }

  const minutes = Math.floor(diff / MINUTE);

  if (minutes < 60) {
    return `${minutes} min ago`;
  }

  const hours = Math.floor(minutes / 60);

  if (hours < 24) {
    return `${hours} ${hours === 1 ? 'hour' : 'hours'} ago`;
  }

  const days = Math.floor(hours / 24);

  if (days < 7) {
    return `${days} ${days === 1 ? 'day' : 'days'} ago`;
  }

  return new Date(iso).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
}

export function badgeText(count: number): string {
  return count > 99 ? '99+' : String(count);
}

export function bellLabel(count: number): string {
  return count === 0 ? 'Notifications, none unread' : `Notifications, ${count} unread`;
}

const CATEGORIES: Record<NotificationType, string> = {
  General: 'Update',
  NewServiceRequest: 'Request',
  QuoteReceived: 'Quote',
  QuoteAccepted: 'Quote',
  AppointmentUpdated: 'Appointment',
  PaymentCompleted: 'Payment',
  ReviewReceived: 'Review',
  ProviderVerified: 'Account',
};

export function categoryLabel(type: NotificationType): string {
  return CATEGORIES[type];
}