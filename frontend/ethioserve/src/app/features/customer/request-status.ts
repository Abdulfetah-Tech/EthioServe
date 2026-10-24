import { RequestStatus } from '../../core/models/request.models';
import { EtbPipe } from '../../shared/etb.pipe';

const etb = new EtbPipe();

export const STATUS_LABELS: Record<RequestStatus, string> = {
  Pending: 'Waiting for quotes',
  Quoted: 'Quotes received',
  Accepted: 'Quote accepted',
  Scheduled: 'Scheduled',
  InProgress: 'In progress',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
  Rejected: 'Rejected',
};

export function statusLabel(status: RequestStatus): string {
  return STATUS_LABELS[status];
}

export function statusBadge(status: RequestStatus): string {
  switch (status) {
    case 'Pending':
      return 'badge badge-warn';
    case 'Cancelled':
    case 'Rejected':
      return 'badge badge-danger';
    case 'Quoted':
    case 'Accepted':
    case 'Scheduled':
    case 'InProgress':
    case 'Completed':
      return 'badge badge-ok';
    default:
      return 'badge';
  }
}

/** Mirrors the API: the details can change only until a provider has responded. */
export function canEdit(status: RequestStatus): boolean {
  return status === 'Pending';
}

/** Mirrors the API: a request can be cancelled while it is open. */
export function canCancel(status: RequestStatus): boolean {
  return status === 'Pending' || status === 'Quoted';
}

export function budgetText(min: number | null, max: number | null): string {
  if (min !== null && max !== null) {
    return `${etb.transform(min)} to ${etb.transform(max)}`;
  }

  if (min !== null) {
    return `From ${etb.transform(min)}`;
  }

  if (max !== null) {
    return `Up to ${etb.transform(max)}`;
  }

  return 'No budget set';
}

/** The preferred date and time as a short phrase, for example "1 Dec 2026 at 14:30". */
export function whenText(date: string | null, time: string | null): string {
  if (!date) {
    return 'Flexible';
  }

  const day = new Date(`${date}T00:00:00`).toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });

  return time ? `${day} at ${time.slice(0, 5)}` : day;
}

export function nextStepText(status: RequestStatus): string {
  switch (status) {
    case 'Pending':
      return 'Verified providers who offer this service in your city can see your request, without your address or contact details. Their quotes will appear here in the next release. Until a provider responds you can still edit or cancel it.';
    case 'Quoted':
      return 'Providers have responded. Compare their quotes and accept the one you like. You can still cancel.';
    case 'Accepted':
    case 'Scheduled':
    case 'InProgress':
      return 'A provider is on the job. You will see the appointment details here.';
    case 'Completed':
      return 'This job is finished.';
    case 'Cancelled':
    case 'Rejected':
      return 'This request is closed. Create a new one if you still need help.';
    default:
      return '';
  }
}