import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export const PASSWORD_HINT = 'At least 10 characters, with an upper case letter, a lower case letter and a digit.';

/** Ethiopian numbers like 0911223344 or +251911223344. Must match the API rule. */
export const PHONE_PATTERN = /^\+?[0-9]{9,15}$/;

/** Mirrors the API password rules, so users learn about problems before submitting. The API still decides. */
export function strongPassword(control: AbstractControl): ValidationErrors | null {
  const value = typeof control.value === 'string' ? control.value : '';

  if (value === '') {
    return null; // "required" reports empty values
  }

  const strong =
    value.length >= 10 && value.length <= 128 && /[a-z]/.test(value) && /[A-Z]/.test(value) && /\d/.test(value);

  return strong ? null : { weakPassword: true };
}

export function matchingFields(first: string, second: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null =>
    group.get(first)?.value === group.get(second)?.value ? null : { mismatch: true };
}