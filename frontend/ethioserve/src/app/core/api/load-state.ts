import { Observable, catchError, map, of, startWith } from 'rxjs';
import { toApiError } from './api-error';

/** Every request a page makes is in exactly one of these states, so a page cannot forget the loading or error case. */
export type LoadState<T> =
  | { kind: 'loading' }
  | { kind: 'error'; message: string; httpStatus: number }
  | { kind: 'ready'; data: T };

export const LOADING: LoadState<never> = { kind: 'loading' };

/** Turns a one-off request into a stream a page can render: loading first, then data or a safe error message. */
export function toLoadState<T>(source: Observable<T>): Observable<LoadState<T>> {
  return source.pipe(
    map((data): LoadState<T> => ({ kind: 'ready', data })),
    catchError((error: unknown) => {
      const apiError = toApiError(error);

      return of<LoadState<T>>({ kind: 'error', message: apiError.message, httpStatus: apiError.status });
    }),
    startWith<LoadState<T>>({ kind: 'loading' }),
  );
}