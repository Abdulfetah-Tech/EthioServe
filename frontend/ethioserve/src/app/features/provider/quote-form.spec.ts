import { FormControl, FormGroup } from '@angular/forms';
import {
  MAX_AMOUNT,
  MAX_MINUTES,
  MIN_MINUTES,
  amountProblem,
  amountRule,
  buildQuoteBody,
  descriptionProblem,
  descriptionRule,
  durationProblem,
  durationRule,
  problemMessage,
  toMinutes,
} from './quote-form';

describe('amountProblem', () => {
  it.each([0.01, 1, 450.5, 500, MAX_AMOUNT])('accepts %s', (amount) => {
    expect(amountProblem(amount)).toBeNull();
  });

  it('asks for a price when there is none', () => {
    expect(amountProblem(null)).toBe('Enter your price.');
    expect(amountProblem(Number.NaN)).toBe('Enter your price.');
  });

  it('rejects zero, negative, too large and over-precise prices', () => {
    expect(amountProblem(0)).toContain('more than 0');
    expect(amountProblem(-5)).toContain('more than 0');
    expect(amountProblem(MAX_AMOUNT + 1)).toContain('more than');
    expect(amountProblem(10.555)).toContain('2 decimal');
  });
});

describe('toMinutes', () => {
  it('converts every unit to whole minutes', () => {
    expect(toMinutes(45, 'minutes')).toBe(45);
    expect(toMinutes(2, 'hours')).toBe(120);
    expect(toMinutes(1.5, 'hours')).toBe(90);
    expect(toMinutes(3, 'days')).toBe(4320);
  });

  it('has no answer for empty, zero, negative or fractional-minute values', () => {
    expect(toMinutes(null, 'hours')).toBeNull();
    expect(toMinutes(0, 'hours')).toBeNull();
    expect(toMinutes(-1, 'hours')).toBeNull();
    expect(toMinutes(0.5, 'minutes')).toBeNull();
    expect(toMinutes(Number.NaN, 'days')).toBeNull();
  });
});

describe('durationProblem', () => {
  it('asks for an estimate when there is none', () => {
    expect(durationProblem(null, 'hours')).toBe('Enter how long the job will take.');
  });

  it('enforces 15 minutes to 30 days', () => {
    expect(durationProblem(MIN_MINUTES - 1, 'minutes')).toContain('at least 15');
    expect(durationProblem(0, 'hours')).toContain('at least 15');
    expect(durationProblem(MIN_MINUTES, 'minutes')).toBeNull();
    expect(durationProblem(MAX_MINUTES / 1440, 'days')).toBeNull();
    expect(durationProblem(31, 'days')).toContain('30 days');
  });

  it('rejects a fraction of a minute', () => {
    expect(durationProblem(20.5, 'minutes')).toContain('whole');
  });
});

describe('descriptionProblem', () => {
  it('needs 10 to 2000 characters after trimming', () => {
    expect(descriptionProblem('short')).toContain('at least 10');
    expect(descriptionProblem('  ' + 'x'.repeat(9) + '  ')).toContain('at least 10');
    expect(descriptionProblem('x'.repeat(10))).toBeNull();
    expect(descriptionProblem('x'.repeat(2000))).toBeNull();
    expect(descriptionProblem('x'.repeat(2001))).toContain('2000');
  });
});

describe('form rules', () => {
  it('report a problem on the control', () => {
    expect(amountRule(new FormControl(null))).toEqual({ problem: 'Enter your price.' });
    expect(amountRule(new FormControl(500))).toBeNull();
    expect(descriptionRule(new FormControl('short'))).not.toBeNull();
    expect(descriptionRule(new FormControl('Parts and labour included.'))).toBeNull();
  });

  it('check the estimate across its number and its unit', () => {
    const group = (value: number | null, unit: string) =>
      new FormGroup({ durationValue: new FormControl(value), durationUnit: new FormControl(unit) });

    expect(durationRule(group(2, 'hours'))).toBeNull();
    expect(durationRule(group(null, 'hours'))).toEqual({ duration: 'Enter how long the job will take.' });
    expect(durationRule(group(5, 'minutes'))).not.toBeNull();
    expect(durationRule(group(60, 'days'))).not.toBeNull();
  });
});

describe('problemMessage', () => {
  it('reads the rule message, and the server message wins', () => {
    const control = new FormControl(null, [amountRule]);

    expect(problemMessage(control)).toBe('Enter your price.');

    control.setErrors({ server: 'The amount is too high.' });

    expect(problemMessage(control)).toBe('The amount is too high.');
  });

  it('has no message for a valid control', () => {
    expect(problemMessage(new FormControl(500, [amountRule]))).toBeNull();
  });
});

describe('buildQuoteBody', () => {
  it('trims the note and converts the estimate to minutes', () => {
    expect(
      buildQuoteBody({ amount: 450.5, description: '  Parts included.  ', durationValue: 2, durationUnit: 'hours', validForDays: 7 }),
    ).toEqual({ amount: 450.5, description: 'Parts included.', estimatedDurationMinutes: 120, validForDays: 7 });
  });
});