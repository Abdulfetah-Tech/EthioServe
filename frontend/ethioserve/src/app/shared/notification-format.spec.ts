import { NotificationType } from '../core/models/notification.models';
import { badgeText, bellLabel, categoryLabel, timeAgo } from './notification-format';

const NOW = Date.parse('2026-12-01T08:00:00Z');
const ago = (ms: number): string => new Date(NOW - ms).toISOString();
const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

describe('timeAgo', () => {
  it('says "Just now" for under a minute, and for a clock that is slightly ahead', () => {
    expect(timeAgo(ago(10_000), NOW)).toBe('Just now');
    expect(timeAgo(ago(-30_000), NOW)).toBe('Just now');
  });

  it('describes minutes, hours and days', () => {
    expect(timeAgo(ago(5 * MINUTE), NOW)).toBe('5 min ago');
    expect(timeAgo(ago(HOUR), NOW)).toBe('1 hour ago');
    expect(timeAgo(ago(3 * HOUR + 20 * MINUTE), NOW)).toBe('3 hours ago');
    expect(timeAgo(ago(DAY), NOW)).toBe('1 day ago');
    expect(timeAgo(ago(6 * DAY), NOW)).toBe('6 days ago');
  });

  it('shows the date after a week', () => {
    expect(timeAgo(ago(8 * DAY), NOW)).toContain('2026');
  });

  it('is empty for an unreadable date', () => {
    expect(timeAgo('nope', NOW)).toBe('');
  });
});

describe('badgeText and bellLabel', () => {
  it('caps the badge at 99+', () => {
    expect(badgeText(1)).toBe('1');
    expect(badgeText(99)).toBe('99');
    expect(badgeText(100)).toBe('99+');
  });

  it('gives a screen reader the whole story', () => {
    expect(bellLabel(0)).toBe('Notifications, none unread');
    expect(bellLabel(3)).toBe('Notifications, 3 unread');
  });
});

describe('categoryLabel', () => {
  it('has a label for every type', () => {
    const types: NotificationType[] = [
      'General', 'NewServiceRequest', 'QuoteReceived', 'QuoteAccepted',
      'AppointmentUpdated', 'PaymentCompleted', 'ReviewReceived', 'ProviderVerified',
    ];

    for (const type of types) {
      expect(categoryLabel(type).length).toBeGreaterThan(0);
    }
  });
});