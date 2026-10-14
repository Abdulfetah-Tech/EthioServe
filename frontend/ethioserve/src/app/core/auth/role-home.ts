import { Roles } from '../models/auth.models';

/** Where a user lands after signing in. */
export function homeRouteFor(roles: readonly string[]): string {
  if (roles.includes(Roles.Admin)) {
    return '/admin';
  }

  if (roles.includes(Roles.Provider)) {
    return '/provider';
  }

  if (roles.includes(Roles.Customer)) {
    return '/customer';
  }

  return '/';
}