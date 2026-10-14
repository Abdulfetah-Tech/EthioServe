import { AbstractControl, FormGroup } from '@angular/forms';
import { ApiError } from './api-error';

/** Puts the API's per-field messages on the matching form controls. Returns messages that match no control. */
export function applyServerErrors(form: FormGroup, error: ApiError): string[] {
  const unmatched: string[] = [];

  for (const [field, messages] of Object.entries(error.fieldErrors)) {
    const control: AbstractControl | null = form.get(field);

    if (control) {
      control.setErrors({ server: messages.join(' ') });
      control.markAsTouched();
    } else {
      unmatched.push(...messages);
    }
  }

  return unmatched;
}