import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { JobRequestBody } from '../../core/models/request.models';

export const MAX_BUDGET = 10_000_000;

// Ethiopia is UTC+3 all year (no daylight saving), the same rule the API uses for "today".
const ADDIS_OFFSET_MS = 3 * 60 * 60 * 1000;

/** Today's date in Addis Ababa as yyyy-MM-dd. */
export function todayInAddis(now: Date = new Date()): string {
  return new Date(now.getTime() + ADDIS_OFFSET_MS).toISOString().slice(0, 10);
}

/** Length is measured after trimming, so a title of spaces never passes. */
export function trimmedLength(min: number, max: number): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const actual = String(control.value ?? '').trim().length;

    return actual >= min && actual <= max ? null : { trimmedLength: { min, max, actual } };
  };
}

/** A blank date is fine; a date before today (in Addis Ababa) is not. */
export function notInPast(today: () => string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = String(control.value ?? '');

    return value === '' || value >= today() ? null : { pastDate: true };
  };
}

/** Rules that involve more than one field. */
export const jobRequestRules: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const min = group.get('budgetMin')?.value as number | null | undefined;
  const max = group.get('budgetMax')?.value as number | null | undefined;
  const date = group.get('preferredDate')?.value as string | null | undefined;
  const time = group.get('preferredTime')?.value as string | null | undefined;
  const errors: ValidationErrors = {};

  if (typeof min === 'number' && typeof max === 'number' && max < min) {
    errors['budgetRange'] = true;
  }

  if (time && !date) {
    errors['timeNeedsDate'] = true;
  }

  return Object.keys(errors).length > 0 ? errors : null;
};

/** The server's message when there is one, otherwise a message for the length rule. */
export function lengthMessage(control: AbstractControl): string | null {
  const server = control.errors?.['server'];

  if (typeof server === 'string') {
    return server;
  }

  const rule = control.errors?.['trimmedLength'] as { min: number; max: number; actual: number } | undefined;

  return rule ? `Use between ${rule.min} and ${rule.max} characters (you have ${rule.actual}).` : null;
}

/** The API expects HH:mm:ss; a time input gives HH:mm. */
export function toApiTime(value: string): string | null {
  if (!value) {
    return null;
  }

  return value.length === 5 ? `${value}:00` : value;
}

export function fromApiTime(value: string | null): string {
  return value ? value.slice(0, 5) : '';
}

export interface JobFormValue {
  title: string;
  description: string;
  address: string;
  regionId: number;
  cityId: number;
  budgetMin: number | null;
  budgetMax: number | null;
  preferredDate: string;
  preferredTime: string;
}

export function buildBody(value: JobFormValue): JobRequestBody {
  const hasDate = value.preferredDate !== '';

  return {
    title: value.title.trim(),
    description: value.description.trim(),
    address: value.address.trim(),
    regionId: value.regionId,
    cityId: value.cityId,
    budgetMin: value.budgetMin,
    budgetMax: value.budgetMax,
    preferredDate: hasDate ? value.preferredDate : null,
    preferredTime: hasDate ? toApiTime(value.preferredTime) : null,
  };
}