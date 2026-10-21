import { VerificationStatus } from '../../core/models/provider.models';

export type AdminAction = 'approve' | 'reject' | 'suspend' | 'reinstate';

export interface ActionInfo {
  action: AdminAction;
  label: string;
  tone: 'primary' | 'danger';
  needsReason: boolean;
  confirmText: string;
  doneText: string;
}

const ACTIONS: Record<AdminAction, ActionInfo> = {
  approve: {
    action: 'approve',
    label: 'Approve',
    tone: 'primary',
    needsReason: false,
    confirmText: 'Approve this provider? They will appear in public search straight away.',
    doneText: 'Provider approved. They now appear in public search.',
  },
  reject: {
    action: 'reject',
    label: 'Reject',
    tone: 'danger',
    needsReason: true,
    confirmText: 'Tell the provider what to fix. They can update their profile and apply again.',
    doneText: 'Provider rejected. They can update their profile and apply again.',
  },
  suspend: {
    action: 'suspend',
    label: 'Suspend',
    tone: 'danger',
    needsReason: true,
    confirmText: 'The provider will disappear from public search until you reinstate them.',
    doneText: 'Provider suspended and hidden from public search.',
  },
  reinstate: {
    action: 'reinstate',
    label: 'Reinstate',
    tone: 'primary',
    needsReason: false,
    confirmText: 'Reinstate this provider? They will appear in public search again.',
    doneText: 'Provider reinstated.',
  },
};

/** The decisions an admin can take in each status. Mirrors the API's state machine, which enforces it regardless. */
export function availableActions(status: VerificationStatus): ActionInfo[] {
  switch (status) {
    case 'Pending':
      return [ACTIONS.approve, ACTIONS.reject];
    case 'Verified':
      return [ACTIONS.suspend];
    case 'Suspended':
      return [ACTIONS.reinstate];
    default:
      return [];
  }
}

/** Shown when no decision is possible because the provider has to act first. */
export function waitingText(status: VerificationStatus): string {
  switch (status) {
    case 'Unverified':
      return 'This provider has not asked for verification yet.';
    case 'Rejected':
      return 'Waiting for the provider to update their profile and apply again.';
    default:
      return '';
  }
}

export function statusBadgeClass(status: VerificationStatus): string {
  switch (status) {
    case 'Verified':
      return 'badge badge-ok';
    case 'Pending':
      return 'badge badge-warn';
    case 'Rejected':
    case 'Suspended':
      return 'badge badge-danger';
    default:
      return 'badge';
  }
}