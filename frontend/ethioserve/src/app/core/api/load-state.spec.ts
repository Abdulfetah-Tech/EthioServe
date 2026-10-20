import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom, of, throwError, toArray } from 'rxjs';
import { toLoadState } from './load-state';

describe('toLoadState', () => {
  it('emits loading first and then the data', async () => {
    const states = await firstValueFrom(toLoadState(of(42)).pipe(toArray()));

    expect(states).toEqual([{ kind: 'loading' }, { kind: 'ready', data: 42 }]);
  });

  it('turns a failed request into a safe error state instead of throwing', async () => {
    const failing = throwError(() => new HttpErrorResponse({ status: 404, error: { detail: 'Provider not found.' } }));

    const states = await firstValueFrom(toLoadState<number>(failing).pipe(toArray()));

    expect(states).toEqual([
      { kind: 'loading' },
      { kind: 'error', message: 'Provider not found.', httpStatus: 404 },
    ]);
  });

  it('never leaks the text of unexpected errors', async () => {
    const failing = throwError(() => new Error('secret internal detail'));

    const states = await firstValueFrom(toLoadState<number>(failing).pipe(toArray()));

    expect(states[states.length - 1].kind).toBe('error');
    expect(JSON.stringify(states)).not.toContain('secret');
  });
});