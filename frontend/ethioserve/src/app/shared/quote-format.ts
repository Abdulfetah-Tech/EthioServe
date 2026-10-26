import { QuoteStatus } from '../core/models/quote.models';

const LABELS: Record<QuoteStatus, string> = {
  Pending: 'Open',
  Accepted: 'Accepted',
  Rejected: 'Not chosen',
  Expired: 'Expired',
  Cancelled: 'Closed',
};

const BADGES: Record<QuoteStatus, string> = {
  Pending: 'badge badge-warn',
  Accepted: 'badge badge-ok',
  Rejected: 'badge',
  Expired: 'badge',
  Cancelled: 'badge',
};

const EXPLANATIONS: Record<QuoteStatus, string> = {
  Pending: 'The customer can still accept this quote.',
  Accepted: 'The customer accepted this quote.',
  Rejected: 'The customer chose another quote, or declined this one.',
  Expired: 'This quote ran out of time. You can send a new one if the request is still open.',
  Cancelled: 'This quote is closed: you withdrew it, or the customer cancelled the request.',
};

export function quoteStatusLabel(status: QuoteStatus): string {
  return LABELS[status];
}

export function quoteStatusBadge(status: QuoteStatus): string {
  return BADGES[status];
}

export function quoteExplanation(status: QuoteStatus): string {
  return EXPLANATIONS[status];
}

/** The API already reports a quote past its deadline as Expired, so "Pending" means the customer can still accept it. */
export function isOpenQuote(status: QuoteStatus): boolean {
  return status === 'Pending';
}

/** The quote a provider should see first: the open one, else the accepted one, else the newest. */
export function currentQuote<T extends { status: QuoteStatus }>(items: readonly T[]): T | null {
  return items.find((q) => q.status === 'Pending') ?? items.find((q) => q.status === 'Accepted') ?? items[0] ?? null;
}

/** The cheapest open quote, but only when there is something to compare it with. */
export function cheapestOpenId<T extends { id: string; amount: number; status: QuoteStatus }>(
  quotes: readonly T[],
): string | null {
  const open = quotes.filter((q) => isOpenQuote(q.status));

  if (open.length < 2) {
    return null;
  }

  return open.reduce((best, q) => (q.amount < best.amount ? q : best)).id;
}

export function durationText(minutes: number): string {
  if (minutes < 60) {
    return `${minutes} min`;
  }

  if (minutes < 1440) {
    const hours = Math.floor(minutes / 60);
    const rest = minutes % 60;

    return rest === 0 ? `${hours} ${hours === 1 ? 'hour' : 'hours'}` : `${hours} h ${rest} min`;
  }

  const days = minutes / 1440;

  return Number.isInteger(days) ? `${days} ${days === 1 ? 'day' : 'days'}` : `${days.toFixed(1)} days`;
}

/** Time left, rounded up, so a quote valid for 7 days reads "7 days" rather than "6 days". */
export function expiryText(validUntil: string, now: number = Date.now()): string {
  const remaining = Date.parse(validUntil) - now;

  if (Number.isNaN(remaining) || remaining <= 0) {
    return 'Expired';
  }

  if (remaining < 60_000) {
    return 'Expires in less than a minute';
  }

  const minutes = Math.ceil(remaining / 60_000);

  if (minutes < 60) {
    return `Expires in ${minutes} min`;
  }

  if (minutes < 1440) {
    const hours = Math.ceil(minutes / 60);

    return `Expires in ${hours} ${hours === 1 ? 'hour' : 'hours'}`;
  }

  const days = Math.ceil(minutes / 1440);

  return `Expires in ${days} ${days === 1 ? 'day' : 'days'}`;
}