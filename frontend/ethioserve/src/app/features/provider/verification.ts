import { MyProfile, VerificationStatus } from '../../core/models/provider.models';

/** Must match the API: it refuses a verification request without these. */
export const MIN_DESCRIPTION_LENGTH = 20;

export interface ChecklistItem {
  key: 'description' | 'address' | 'service';
  label: string;
  done: boolean;
  link: string;
}

export function verificationChecklist(
  profile: Pick<MyProfile, 'description' | 'address' | 'activeServiceCount'>,
): ChecklistItem[] {
  return [
    {
      key: 'description',
      label: `Write a description of at least ${MIN_DESCRIPTION_LENGTH} characters`,
      done: profile.description.trim().length >= MIN_DESCRIPTION_LENGTH,
      link: '/provider/profile',
    },
    {
      key: 'address',
      label: 'Add your address',
      done: profile.address.trim().length > 0,
      link: '/provider/profile',
    },
    {
      key: 'service',
      label: 'Offer at least one service',
      done: profile.activeServiceCount > 0,
      link: '/provider/services',
    },
  ];
}

/** A provider can ask for review when they are new or were rejected, and everything on the checklist is done. */
export function canRequestVerification(
  profile: Pick<MyProfile, 'verificationStatus'>,
  checklist: readonly ChecklistItem[],
): boolean {
  return needsSetup(profile.verificationStatus) && checklist.every((item) => item.done);
}

export function needsSetup(status: VerificationStatus): boolean {
  return status === 'Unverified' || status === 'Rejected';
}

export const STATUS_INFO: Record<VerificationStatus, { title: string; text: string }> = {
  Unverified: {
    title: 'Not verified yet',
    text: 'Complete the checklist below, then ask us to verify your profile. Customers can only find verified providers.',
  },
  Pending: {
    title: 'Verification pending',
    text: 'We are reviewing your profile. You will appear in search results as soon as it is approved.',
  },
  Verified: {
    title: 'Verified',
    text: 'Customers can find you in search and see your services and prices.',
  },
  Rejected: {
    title: 'Verification rejected',
    text: 'Please update your profile using the message below, then request verification again.',
  },
  Suspended: {
    title: 'Suspended',
    text: 'You are not shown to customers right now. Please read the message below and contact support.',
  },
};