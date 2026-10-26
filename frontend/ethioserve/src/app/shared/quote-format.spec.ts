import { QuoteStatus } from '../core/models/quote.models';
import {
  cheapestOpenId,
  currentQuote,
  durationText,
  expiryText,
  isOpenQuote,
  quoteExplanation,
  quoteStatusBadge,
  quoteStatusLabel,
} from './quote-format';

const ALL: QuoteStatus[] = ['Pending', 'Accepted', 'Rejected', 'Expired', 'Cancelled'];

const quote = (id: string, amount: number, status: QuoteStatus) => ({ id, amount, status });

describe('status helpers', () => {
  it('have a label, a badge and an explanation for every status', () => {
    for (const status of ALL) {
      expect(quoteStatusLabel(status).length).toBeGreaterThan(0);
      expect(quoteStatusBadge(status)).toContain('badge');
      expect(quoteExplanation(status).length).toBeGreaterThan(0);
    }
  });

  it('colours open quotes amber and accepted ones green', () => {
    expect(quoteStatusBadge('Pending')).toBe('badge badge-warn');
    expect(quoteStatusBadge('Accepted')).toBe('badge badge-ok');
  });

  it('treats only a pending quote as open', () => {
    expect(ALL.filter((status) => isOpenQuote(status))).toEqual(['Pending']);
  });
});

describe('currentQuote', () => {
  it('has nothing for an empty list', () => {
    expect(currentQuote([])).toBeNull();
  });

  it('prefers the open quote over a newer finished one', () => {
    const items = [quote('new', 100, 'Expired'), quote('open', 200, 'Pending')];

    expect(currentQuote(items)?.id).toBe('open');
  });

  it('prefers the accepted quote when none is open, and otherwise the newest', () => {
    expect(currentQuote([quote('a', 1, 'Rejected'), quote('b', 2, 'Accepted')])?.id).toBe('b');
    expect(currentQuote([quote('first', 1, 'Rejected'), quote('second', 2, 'Expired')])?.id).toBe('first');
  });
});

describe('cheapestOpenId', () => {
  it('needs at least two open quotes to compare', () => {
    expect(cheapestOpenId([])).toBeNull();
    expect(cheapestOpenId([quote('a', 100, 'Pending')])).toBeNull();
    expect(cheapestOpenId([quote('a', 100, 'Pending'), quote('b', 50, 'Rejected')])).toBeNull();
  });

  it('picks the cheapest open quote and ignores finished ones', () => {
    const items = [quote('cheap-but-closed', 10, 'Expired'), quote('a', 300, 'Pending'), quote('b', 250, 'Pending')];

    expect(cheapestOpenId(items)).toBe('b');
  });

  it('picks the first of equal prices', () => {
    expect(cheapestOpenId([quote('a', 100, 'Pending'), quote('b', 100, 'Pending')])).toBe('a');
  });
});

describe('durationText', () => {
  it.each([
    [15, '15 min'],
    [45, '45 min'],
    [60, '1 hour'],
    [120, '2 hours'],
    [90, '1 h 30 min'],
    [1440, '1 day'],
    [4320, '3 days'],
    [2160, '1.5 days'],
  ])('describes %s minutes as "%s"', (minutes, expected) => {
    expect(durationText(minutes)).toBe(expected);
  });
});

describe('expiryText', () => {
  const now = Date.parse('2026-10-26T08:00:00Z');
  const at = (ms: number): string => new Date(now + ms).toISOString();
  const MINUTE = 60_000;
  const HOUR = 60 * MINUTE;
  const DAY = 24 * HOUR;

  it('says expired at or after the deadline, and for an unreadable date', () => {
    expect(expiryText(at(-1000), now)).toBe('Expired');
    expect(expiryText(at(0), now)).toBe('Expired');
    expect(expiryText('not a date', now)).toBe('Expired');
  });

  it('describes the time left, rounded up', () => {
    expect(expiryText(at(30_000), now)).toBe('Expires in less than a minute');
    expect(expiryText(at(5 * MINUTE), now)).toBe('Expires in 5 min');
    expect(expiryText(at(HOUR), now)).toBe('Expires in 1 hour');
    expect(expiryText(at(90 * MINUTE), now)).toBe('Expires in 2 hours');
    expect(expiryText(at(3 * HOUR), now)).toBe('Expires in 3 hours');
    expect(expiryText(at(DAY), now)).toBe('Expires in 1 day');
    expect(expiryText(at(3 * DAY), now)).toBe('Expires in 3 days');
  });

  it('shows a quote valid for 7 days as 7 days even a moment after it was sent', () => {
    expect(expiryText(at(7 * DAY - 1000), now)).toBe('Expires in 7 days');
  });
});