import { VerificationStatus } from '../../core/models/provider.models';
import { availableActions, statusBadgeClass, waitingText } from './provider-actions';

const names = (status: VerificationStatus): string[] => availableActions(status).map((info) => info.action);

describe('availableActions', () => {
  it('offers approve and reject for a pending provider', () => {
    expect(names('Pending')).toEqual(['approve', 'reject']);
  });

  it('offers only suspend for a verified provider and only reinstate for a suspended one', () => {
    expect(names('Verified')).toEqual(['suspend']);
    expect(names('Suspended')).toEqual(['reinstate']);
  });

  it('offers nothing while the provider has to act first', () => {
    expect(names('Unverified')).toEqual([]);
    expect(names('Rejected')).toEqual([]);
  });

  it('asks for a reason only when rejecting or suspending', () => {
    const byAction = Object.fromEntries(
      (['Pending', 'Verified', 'Suspended'] as const).flatMap((status) => availableActions(status)).map((info) => [info.action, info.needsReason]),
    );

    expect(byAction).toEqual({ approve: false, reject: true, suspend: true, reinstate: false });
  });

  it('marks the destructive decisions as dangerous', () => {
    const tones = Object.fromEntries(
      (['Pending', 'Verified', 'Suspended'] as const).flatMap((status) => availableActions(status)).map((info) => [info.action, info.tone]),
    );

    expect(tones).toEqual({ approve: 'primary', reject: 'danger', suspend: 'danger', reinstate: 'primary' });
  });
});

describe('waitingText', () => {
  it('explains who has to act when no decision is possible', () => {
    expect(waitingText('Unverified')).toContain('not asked for verification');
    expect(waitingText('Rejected')).toContain('update their profile');
    expect(waitingText('Pending')).toBe('');
  });
});

describe('statusBadgeClass', () => {
  it('colours each status', () => {
    expect(statusBadgeClass('Verified')).toBe('badge badge-ok');
    expect(statusBadgeClass('Pending')).toBe('badge badge-warn');
    expect(statusBadgeClass('Rejected')).toBe('badge badge-danger');
    expect(statusBadgeClass('Suspended')).toBe('badge badge-danger');
    expect(statusBadgeClass('Unverified')).toBe('badge');
  });
});