import { HttpErrorResponse } from '@angular/common/http';

interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  traceId?: string;
  errors?: Record<string, string[]>;
}

/** What the UI works with: a safe message, plus per-field messages for forms. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly fieldErrors: Record<string, string[]> = {},
    readonly traceId?: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return new ApiError(0, 'Cannot reach the server. Check your connection and try again.');
    }

    const problem: ProblemDetails =
      typeof error.error === 'object' && error.error !== null ? (error.error as ProblemDetails) : {};

    return new ApiError(
      error.status,
      problem.detail ?? problem.title ?? defaultMessage(error.status),
      normalizeFieldErrors(problem.errors),
      problem.traceId,
    );
  }

  return new ApiError(-1, 'Something went wrong. Please try again.');
}

// The API names fields like "Password"; form controls use "password".
function normalizeFieldErrors(errors: Record<string, string[]> | undefined): Record<string, string[]> {
  const result: Record<string, string[]> = {};

  for (const [key, messages] of Object.entries(errors ?? {})) {
    result[key.charAt(0).toLowerCase() + key.slice(1)] = messages;
  }

  return result;
}

function defaultMessage(status: number): string {
  switch (status) {
    case 400:
      return 'Some of the information you entered is not valid.';
    case 401:
      return 'Please sign in to continue.';
    case 403:
      return 'You do not have permission to do that.';
    case 404:
      return 'The requested item was not found.';
    case 409:
      return 'This conflicts with existing data.';
    case 429:
      return 'Too many attempts. Please wait a minute and try again.';
    default:
      return 'Something went wrong on our side. Please try again later.';
  }
}