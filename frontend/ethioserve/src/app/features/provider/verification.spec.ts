import { MyProfile } from '../../core/models/provider.models';
import { STATUS_INFO, canRequestVerification, needsSetup, verificationChecklist } from './verification';

const complete: Pick<MyProfile, 'description' | 'address' | 'activeServiceCount'> = {
  description: 'x'.repeat(20),
  address: 'Bole, near the main road',
  activeServiceCount: 1,
};

describe('verificationChecklist', () => {
  it('is fully done for a complete profile', () => {
    expect(verificationChecklist(complete).every((item) => item.done)).toBe(true);
  });

  it('flags a description that is one character too short', () => {
    const item = verificationChecklist({ ...complete, description: 'x'.repeat(19) }).find((i) => i.key === 'description');

    expect(item?.done).toBe(false);
  });

  it('ignores surrounding spaces when counting the description', () => {
    const item = verificationChecklist({ ...complete, description: `   ${'x'.repeat(19)}   ` }).find((i) => i.key === 'description');

    expect(item?.done).toBe(false);
  });

  it('flags a blank address', () => {
    expect(verificationChecklist({ ...complete, address: '   ' }).find((i) => i.key === 'address')?.done).toBe(false);
  });

  it('flags a provider without an active service and points to the right page', () => {
    const item = verificationChecklist({ ...complete, activeServiceCount: 0 }).find((i) => i.key === 'service');

    expect(item?.done).toBe(false);
    expect(item?.link).toBe('/provider/services');
  });
});

describe('canRequestVerification', () => {
  const done = verificationChecklist(complete);
  const incomplete = verificationChecklist({ ...complete, activeServiceCount: 0 });

  it('is allowed for new and rejected providers with a complete profile', () => {
    expect(canRequestVerification({ verificationStatus: 'Unverified' }, done)).toBe(true);
    expect(canRequestVerification({ verificationStatus: 'Rejected' }, done)).toBe(true);
  });

  it('is not allowed while pending, verified or suspended', () => {
    expect(canRequestVerification({ verificationStatus: 'Pending' }, done)).toBe(false);
    expect(canRequestVerification({ verificationStatus: 'Verified' }, done)).toBe(false);
    expect(canRequestVerification({ verificationStatus: 'Suspended' }, done)).toBe(false);
  });

  it('is not allowed with an incomplete checklist', () => {
    expect(canRequestVerification({ verificationStatus: 'Unverified' }, incomplete)).toBe(false);
  });
});

describe('needsSetup and STATUS_INFO', () => {
  it('shows the checklist only before and after a review', () => {
    expect(needsSetup('Unverified')).toBe(true);
    expect(needsSetup('Rejected')).toBe(true);
    expect(needsSetup('Pending')).toBe(false);
  });

  it('has a message for every status', () => {
    expect(Object.keys(STATUS_INFO).sort()).toEqual(['Pending', 'Rejected', 'Suspended', 'Unverified', 'Verified']);
  });
});