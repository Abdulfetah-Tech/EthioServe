import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { homeRouteFor } from '../auth/role-home';

/** Signed-in users only. These guards improve the experience; the API enforces access on every call regardless. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.isAuthenticated()
    ? true
    : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Login and register pages: signed-in users are sent to their own area instead. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.isAuthenticated() ? router.createUrlTree([homeRouteFor(auth.roles())]) : true;
};

export const roleGuard =
  (...allowedRoles: string[]): CanActivateFn =>
  (_route, state) => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!auth.isAuthenticated()) {
      return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
    }

    return allowedRoles.some((role) => auth.hasRole(role)) ? true : router.createUrlTree(['/forbidden']);
  };