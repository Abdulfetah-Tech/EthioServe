import { HttpErrorResponse } from '@angular/common/http';
import { ApiError, toApiError } from './api-error';

describe('toApiError', () => {
  it('uses the problem detail as the message', () => {
    const result = toApiError(
      new HttpErrorResponse({
        status: 401,
        error: { title: 'Authentication failed', detail: 'Invalid email or password.' },
      }),
    );

    expect(result.status).toBe(401);
    expect(result.message).toBe('Invalid email or password.');
  });

  it('camel-cases server field names so they match form controls', () => {
    const result = toApiError(
      new HttpErrorResponse({
        status: 400,
        error: { title: 'Validation failed', errors: { Password: ['Too short.'], PhoneNumber: ['Invalid.'] } },
      }),
    );

    expect(result.fieldErrors).toEqual({ password: ['Too short.'], phoneNumber: ['Invalid.'] });
  });

  it('explains a network failure', () => {
    const result = toApiError(new HttpErrorResponse({ status: 0 }));

    expect(result.status).toBe(0);
    expect(result.message).toContain('Cannot reach the server');
  });

  it('never shows a raw server body as the message', () => {
    const result = toApiError(new HttpErrorResponse({ status: 500, error: '<html>stack trace</html>' }));

    expect(result.message).toContain('Something went wrong');
    expect(result.message).not.toContain('stack trace');
  });

  it('passes an ApiError through unchanged', () => {
    const original = new ApiError(409, 'Conflict');

    expect(toApiError(original)).toBe(original);
  });

  it('hides the message of unknown errors', () => {
    const result = toApiError(new Error('secret internal detail'));

    expect(result.message).not.toContain('secret');
  });
});