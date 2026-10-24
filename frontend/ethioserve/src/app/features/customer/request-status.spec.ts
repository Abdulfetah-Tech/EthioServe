import { RequestStatus } from '../../core/models/request.models';
import { budgetText, canCancel, canEdit, nextStepText, statusBadge, statusLabel, whenText } from './request-status';

const ALL: RequestStatus[] = ['Pending', 'Quoted', 'Accepted', 'Scheduled', 'InProgress', 'Completed', 'Cancelled', 'Rejected'];

describe('request status helpers', () => {
  it('has a label and a next-step text for every status', () => {
    for (const status of ALL) {
      expect(statusLabel(status).length).toBeGreaterThan(0);
      expect(nextStepText(status).length).toBeGreaterThan(0);
    }
  });

  it('allows editing only while pending', () => {
    expect(ALL.filter((status) => canEdit(status))).toEqual(['Pending']);
  });

  it('allows cancelling only while the request is open', () => {
    expect(ALL.filter((status) => canCancel(status))).toEqual(['Pending', 'Quoted']);
  });

  it('colours each status', () => {
    expect(statusBadge('Pending')).toBe('badge badge-warn');
    expect(statusBadge('Cancelled')).toBe('badge badge-danger');
    expect(statusBadge('Rejected')).toBe('badge badge-danger');
    expect(statusBadge('Quoted')).toBe('badge badge-ok');
    expect(statusBadge('Completed')).toBe('badge badge-ok');
  });
});

describe('budgetText', () => {
  it('describes a range, one end of a range, or no budget', () => {
    const range = budgetText(500, 1500);

    expect(range).toContain('500');
    expect(range).toContain('1,500');
    expect(range).toContain(' to ');
    expect(budgetText(500, null)).toMatch(/^From /);
    expect(budgetText(null, 800)).toMatch(/^Up to /);
    expect(budgetText(null, null)).toBe('No budget set');
  });
});

describe('whenText', () => {
  it('says flexible when there is no date', () => {
    expect(whenText(null, null)).toBe('Flexible');
    expect(whenText(null, '14:30:00')).toBe('Flexible');
  });

  it('shows the date, and the time when there is one', () => {
    const dateOnly = whenText('2026-12-01', null);

    expect(dateOnly).toContain('2026');
    expect(dateOnly).toContain('Dec');
    expect(dateOnly).not.toContain(' at ');
    expect(whenText('2026-12-01', '14:30:00')).toContain(' at 14:30');
  });
});