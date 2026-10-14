import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

// Marks a request that was already retried once, so a persistent 401 can never cause an endless loop.
const ALREADY_RETRIED = new HttpContextToken<boolean>(() => false);

const AUTH_ENDPOINT = /^\/api\/auth\/(login|register|refresh-token|logout|forgot-password|reset-password)/;

function withToken(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;
}

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  // Only our own API gets the token, and the auth endpoints never trigger a refresh themselves.
  if (!request.url.startsWith('/api/') || AUTH_ENDPOINT.test(request.url)) {
    return next(request);
  }

  return next(withToken(request, auth.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || request.context.get(ALREADY_RETRIED)) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        // Only a failed REFRESH ends the session; a failure of the retried request is passed on untouched.
        catchError((refreshError: unknown) => {
          auth.clearSession();
          void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });

          return throwError(() => refreshError);
        }),
        switchMap((token) =>
          next(
            withToken(
              request.clone({ context: request.context.set(ALREADY_RETRIED, true) }),
              token,
            ),
          ),
        ),
      );
    }),
  );
};