import { FormControl, FormGroup } from '@angular/forms';
import {
  buildBody,
  fromApiTime,
  jobRequestRules,
  lengthMessage,
  notInPast,
  toApiTime,
  todayInAddis,
  trimmedLength,
} from './request-form';

describe('todayInAddis', () => {
  it('uses Addis Ababa time (UTC+3), so the date changes at 21:00 UTC', () => {
    expect(todayInAddis(new Date('2026-10-24T20:59:59Z'))).toBe('2026-10-24');
    expect(todayInAddis(new Date('2026-10-24T21:00:00Z'))).toBe('2026-10-25');
  });
});

describe('trimmedLength', () => {
  const rule = trimmedLength(5, 10);

  it('measures the length after trimming', () => {
    expect(rule(new FormControl('  Pipe  '))).toEqual({ trimmedLength: { min: 5, max: 10, actual: 4 } });
    expect(rule(new FormControl('Pipes'))).toBeNull();
    expect(rule(new FormControl('  Pipes  '))).toBeNull();
  });

  it('rejects blank and too long values', () => {
    expect(rule(new FormControl(''))).not.toBeNull();
    expect(rule(new FormControl('   '))).not.toBeNull();
    expect(rule(new FormControl('x'.repeat(11)))).not.toBeNull();
  });
});

describe('lengthMessage', () => {
  it('describes the length rule and prefers the server message', () => {
    const control = new FormControl('abc', [trimmedLength(5, 150)]);

    expect(lengthMessage(control)).toBe('Use between 5 and 150 characters (you have 3).');

    control.setErrors({ server: 'Already taken.' });

    expect(lengthMessage(control)).toBe('Already taken.');
  });

  it('has no message when the value is valid', () => {
    expect(lengthMessage(new FormControl('Valid title', [trimmedLength(5, 150)]))).toBeNull();
  });
});

describe('notInPast', () => {
  const rule = notInPast(() => '2026-10-24');

  it('allows a blank date, today and the future', () => {
    expect(rule(new FormControl(''))).toBeNull();
    expect(rule(new FormControl('2026-10-24'))).toBeNull();
    expect(rule(new FormControl('2027-01-01'))).toBeNull();
  });

  it('rejects yesterday', () => {
    expect(rule(new FormControl('2026-10-23'))).toEqual({ pastDate: true });
  });
});

describe('jobRequestRules', () => {
  const group = (min: number | null, max: number | null, date: string, time: string) =>
    new FormGroup({
      budgetMin: new FormControl(min),
      budgetMax: new FormControl(max),
      preferredDate: new FormControl(date),
      preferredTime: new FormControl(time),
    });

  it('accepts open and consistent combinations', () => {
    expect(jobRequestRules(group(null, null, '', ''))).toBeNull();
    expect(jobRequestRules(group(100, 100, '2026-12-01', '14:30'))).toBeNull();
    expect(jobRequestRules(group(100, null, '2026-12-01', ''))).toBeNull();
  });

  it('rejects a maximum below the minimum', () => {
    expect(jobRequestRules(group(900, 100, '', ''))).toEqual({ budgetRange: true });
  });

  it('rejects a time without a date', () => {
    expect(jobRequestRules(group(null, null, '', '14:30'))).toEqual({ timeNeedsDate: true });
  });
});

describe('time conversion', () => {
  it('adds seconds for the API and removes them for the time input', () => {
    expect(toApiTime('14:30')).toBe('14:30:00');
    expect(toApiTime('14:30:00')).toBe('14:30:00');
    expect(toApiTime('')).toBeNull();
    expect(fromApiTime('14:30:00')).toBe('14:30');
    expect(fromApiTime(null)).toBe('');
  });
});

describe('buildBody', () => {
  const value = {
    title: '  Leaking pipe  ',
    description: '  The pipe leaks.  ',
    address: '  Bole  ',
    regionId: 1,
    cityId: 2,
    budgetMin: null,
    budgetMax: 800,
    preferredDate: '2026-12-01',
    preferredTime: '14:30',
  };

  it('trims text and converts the time', () => {
    expect(buildBody(value)).toEqual({
      title: 'Leaking pipe',
      description: 'The pipe leaks.',
      address: 'Bole',
      regionId: 1,
      cityId: 2,
      budgetMin: null,
      budgetMax: 800,
      preferredDate: '2026-12-01',
      preferredTime: '14:30:00',
    });
  });

  it('sends no date and no time when no date was chosen', () => {
    const body = buildBody({ ...value, preferredDate: '', preferredTime: '14:30' });

    expect(body.preferredDate).toBeNull();
    expect(body.preferredTime).toBeNull();
  });
});