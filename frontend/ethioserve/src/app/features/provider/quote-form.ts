import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { SubmitQuoteBody } from '../../core/models/quote.models';

/** These limits mirror the API, which still decides. */
export const MAX_AMOUNT = 10_000_000;
export const MIN_MINUTES = 15;
export const MAX_MINUTES = 43_200;
export const MIN_DESCRIPTION = 10;
export const MAX_DESCRIPTION = 2000;

export type DurationUnit = 'minutes' | 'hours' | 'days';

export const DURATION_UNITS: readonly { value: DurationUnit; label: string }[] = [
  { value: 'minutes', label: 'minutes' },
  { value: 'hours', label: 'hours' },
  { value: 'days', label: 'days' },
];

const UNIT_MINUTES: Record<DurationUnit, number> = { minutes: 1, hours: 60, days: 1440 };

export const VALID_FOR_OPTIONS: readonly number[] = [1, 3, 7, 14, 30];

export function amountProblem(amount: number | null): string | null {
  if (amount === null || Number.isNaN(amount)) {
    return 'Enter your price.';
  }

  if (amount < 0.01) {
    return 'The price must be more than 0.';
  }

  if (amount > MAX_AMOUNT) {
    return `The price cannot be more than ${MAX_AMOUNT.toLocaleString('en-US')}.`;
  }

  if (Math.abs(amount * 100 - Math.round(amount * 100)) > 1e-6) {
    return 'Use at most 2 decimal places.';
  }

  return null;
}

/** The estimate in whole minutes, or null when it is empty, not positive, or not a whole number of minutes. */
export function toMinutes(value: number | null, unit: DurationUnit): number | null {
  if (value === null || !Number.isFinite(value) || value <= 0) {
    return null;
  }

  const minutes = value * UNIT_MINUTES[unit];

  return Math.abs(minutes - Math.round(minutes)) > 1e-6 ? null : Math.round(minutes);
}

export function durationProblem(value: number | null, unit: DurationUnit): string | null {
  if (value === null || Number.isNaN(value)) {
    return 'Enter how long the job will take.';
  }

  const minutes = value * UNIT_MINUTES[unit];

  if (Math.abs(minutes - Math.round(minutes)) > 1e-6) {
    return 'Use a whole number of minutes.';
  }

  if (Math.round(minutes) < MIN_MINUTES) {
    return `The job must take at least ${MIN_MINUTES} minutes.`;
  }

  if (Math.round(minutes) > MAX_MINUTES) {
    return 'The estimate cannot be more than 30 days.';
  }

  return null;
}

export function descriptionProblem(text: string): string | null {
  const length = text.trim().length;

  if (length < MIN_DESCRIPTION) {
    return `Describe what the price includes (at least ${MIN_DESCRIPTION} characters).`;
  }

  if (length > MAX_DESCRIPTION) {
    return `The description cannot be longer than ${MAX_DESCRIPTION} characters.`;
  }

  return null;
}

export const amountRule: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const problem = amountProblem(control.value as number | null);

  return problem ? { problem } : null;
};

export const descriptionRule: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const problem = descriptionProblem(String(control.value ?? ''));

  return problem ? { problem } : null;
};

/** The estimate depends on two fields (the number and its unit), so the rule sits on the group. */
export const durationRule: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const value = group.get('durationValue')?.value as number | null | undefined;
  const unit = group.get('durationUnit')?.value as DurationUnit | undefined;
  const problem = durationProblem(value ?? null, unit ?? 'hours');

  return problem ? { duration: problem } : null;
};

/** The server's message when there is one, otherwise the rule's message. */
export function problemMessage(control: AbstractControl): string | null {
  const server = control.errors?.['server'];

  if (typeof server === 'string') {
    return server;
  }

  const problem = control.errors?.['problem'];

  return typeof problem === 'string' ? problem : null;
}

export interface QuoteFormValue {
  amount: number | null;
  description: string;
  durationValue: number | null;
  durationUnit: DurationUnit;
  validForDays: number;
}

export function buildQuoteBody(value: QuoteFormValue): SubmitQuoteBody {
  return {
    amount: Number(value.amount),
    description: value.description.trim(),
    estimatedDurationMinutes: toMinutes(value.durationValue, value.durationUnit) ?? 0,
    validForDays: value.validForDays,
  };
}